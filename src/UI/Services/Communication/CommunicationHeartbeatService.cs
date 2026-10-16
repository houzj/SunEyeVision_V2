using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Communication;

public sealed class CommunicationHeartbeatService : IAsyncDisposable
{
    private readonly CommunicationRuntimeManager _runtime;
    public CommunicationHeartbeatRunner Runner { get; } = new();
    public CommunicationHeartbeatService(CommunicationRuntimeManager runtime) => _runtime = runtime;

    public void Update(Solution? solution) => Runner.Update(CreateJobs(solution));

    public IReadOnlyList<CommunicationHeartbeatJob> CreateJobs(Solution? solution)
    {
        // Capture on the caller's configuration thread; background jobs never enumerate live UI lists.
        var jobs = new List<CommunicationHeartbeatJob>();
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<Guid>();
        if (solution != null)
        {
            foreach (var definition in solution.CommunicationHeartbeats.Where(item => item.Enabled))
            {
                try
                {
                if (definition.IntervalMs < 1) throw new FormatException("心跳间隔必须大于 0ms。");
                var configuredDevice = solution.Communications.FirstOrDefault(device => device.Id == definition.DeviceId)
                    ?? throw new InvalidOperationException("心跳绑定设备已删除。");
                if (!configuredDevice.IsEnabled) continue;
                var device = JsonSerializer.Deserialize<SunEyeVision.Workflow.Communication>(
                    JsonSerializer.Serialize(configuredDevice, WorkflowSerializationOptions.Default), WorkflowSerializationOptions.Default)!;
                var config = JsonSerializer.Deserialize<CommunicationHeartbeatDefinition>(JsonSerializer.Serialize(definition))!;
                if (config.Mode is not ("Single" or "Alternate")) throw new FormatException("心跳模式无效。");
                // Parse both configured values before enabling; OPC UA permissions are rechecked at each send.
                foreach (var content in config.Mode == "Alternate" ? new[] { config.ContentA, config.ContentB } : new[] { config.ContentA })
                    CommunicationSendEventFormatter.Build(CreateMessage(config, content));
                if (device.IsRawProtocol && config.DataType != "string") throw new FormatException("普通文本设备心跳类型应为 string。");
                if (!device.IsRawProtocol && string.IsNullOrEmpty(config.TargetId)) throw new FormatException("请选择心跳地址或数据点。");
                var sender = new CommunicationSendEventService(_runtime);
                if (!device.IsOpcUa)
                    foreach (var content in config.Mode == "Alternate" ? new[] { config.ContentA, config.ContentB } : new[] { config.ContentA })
                        sender.Prepare(CreateMessage(config, content), device);
                else if (!(device.OpcUaDataPoints ?? new()).Any(point => point.Id == config.TargetId))
                    throw new FormatException("心跳数据点已删除。");
                var targetKey = device.IsRawProtocol ? config.DeviceId : config.DeviceId + "/" + config.TargetId;
                if (ids.Contains(config.Id) || targets.Contains(targetKey)) throw new FormatException("心跳 ID 或设备目标不能重复。");
                ids.Add(config.Id);
                targets.Add(targetKey);
                jobs.Add(new(config.Id, targetKey, config.IntervalMs, config.Mode == "Alternate",
                    async (second, token) =>
                    {
                        if (!device.IsEnabled || !_runtime.IsConnected(device.Id)) return false;
                        var plan = sender.Prepare(CreateMessage(config, second ? config.ContentB : config.ContentA), device);
                        return await sender.SendAsync(plan, token).ConfigureAwait(false) > 0;
                    }));
                }
                catch (Exception ex) { Runner.Publish(definition.Id, $"配置无效：{ex.Message}"); }
            }
        }
        return jobs;
    }

    private static CommunicationSendEventDefinition CreateMessage(CommunicationHeartbeatDefinition config, string content) => new()
    {
        DeviceId = config.DeviceId, TargetId = config.TargetId, Separator = "",
        Parameters = new() { new() { Name = "heartbeat", DataType = config.DataType, Content = content } }
    };

    public Task StopAsync() => Runner.StopAsync();
    public ValueTask DisposeAsync() => Runner.DisposeAsync();
}
