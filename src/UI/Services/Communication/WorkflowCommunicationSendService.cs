using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Communication;

/// <summary>No controls or scheduling. Reuses the application's existing connection and send core.</summary>
public sealed class WorkflowCommunicationSendService(Func<Solution?> solutionReader, CommunicationRuntimeManager runtime,
    Func<Solution, string, CancellationToken>? lifetimeReader = null) : ICommunicationSendService
{
    private readonly CommunicationSendEventService _events = new(runtime);
    private Solution Solution => solutionReader() ?? throw new InvalidOperationException("当前通信方案不可用。");
    public IReadOnlyList<CommunicationSendTarget> GetTargets()
    {
        var solution = Solution; var targets = new List<CommunicationSendTarget>();
        foreach (var device in solution.Communications.Where(d => d.IsEnabled))
        {
            if (device.IsRawProtocol) targets.Add(new("Device", "", device.Id, device.Name, "Raw", "Binary"));
            else if (device.IsModbus)
                foreach (var p in device.ModbusAddressPoints.Where(p => p.AccessMode != "只读"))
                    targets.Add(new("Device", p.Id, device.Id, p.Name, "Modbus", "Native", DeviceName: device.Name));
            else if (device.IsOpcUa)
                foreach (var p in device.OpcUaDataPoints)
                    targets.Add(new("Device", p.Id, device.Id, p.Name, "OpcUa", "Native", DeviceName: device.Name));
        }
        var events = solution.CommunicationSendEvents.Select(e => new
        {
            Event = e,
            DeviceName = solution.Communications.FirstOrDefault(d => d.Id == e.DeviceId)?.Name,
            Label = CommunicationSendEventNames.DisplayName(e.Name, e.Mode) + " · " +
                (solution.Communications.FirstOrDefault(d => d.Id == e.DeviceId) is { } device && !string.IsNullOrWhiteSpace(device.Name)
                    ? device.Name : string.IsNullOrWhiteSpace(e.DeviceId) ? "未绑定设备" : "设备已失效")
        }).ToArray();
        var repeated = events.GroupBy(e => e.Label, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var labels = events.ToDictionary(e => e.Event.EventId, e => repeated.Contains(e.Label)
            ? e.Label + " · " + e.Event.EventId.ToString("N")[..8] : e.Label);
        var shortCollisions = labels.GroupBy(e => e.Value, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).SelectMany(g => g.Select(e => e.Key)).ToHashSet();
        foreach (var item in events)
        {
            var e = item.Event;
            var label = shortCollisions.Contains(e.EventId) ? item.Label + " · " + e.EventId.ToString("N") : labels[e.EventId];
            targets.Add(new("Event", e.EventId.ToString(), e.DeviceId, label, "Event", "Native",
                e.Parameters.Select(p => new CommunicationSendParameter(p.FieldId, p.Name, p.DataType)).ToArray()));
        }
        foreach (var v in solution.GlobalVariables.Where(v => v.Type is "String" or "Integer" or "Double" or "Boolean" or "Binary"))
            targets.Add(new("Variable", v.Id, "", "全局." + v.Name, "Variable", v.Type));
        return targets;
    }

    public void ConfigureVariablePublications(string nodeId, IReadOnlyList<SendVariablePublication> mappings)
        => ConfigureVariablePublications("", nodeId, mappings);

    public void ConfigureVariablePublications(string workflowId, string nodeId, IReadOnlyList<SendVariablePublication> mappings)
    {
        var solution = Solution;
        var owners = solution.Workflows.Where(w => (string.IsNullOrEmpty(workflowId) || w.Id == workflowId) && w.Nodes.Any(n => n.Id == nodeId)).ToArray();
        if (owners.Length != 1) throw new InvalidOperationException("无法确定发送节点工作流作用域。");
        if (mappings.Select(m => m.VariableId).Distinct().Count() != mappings.Count) throw new InvalidOperationException("同一变量不能重复配置。");
        var pending = mappings.Select(m => (Mapping: m, Variable: solution.GlobalVariables.FirstOrDefault(v => v.Id == m.VariableId)
            ?? throw new InvalidOperationException("目标变量不存在。"))).ToArray();
        foreach (var (mapping, variable) in pending)
        {
            if (variable.Type != mapping.DataType) throw new InvalidOperationException("目标变量类型已变化。");
            if (variable.ProducerBinding is { } producer && (producer.NodeId != nodeId || producer.WorkflowId != owners[0].Id))
                throw new InvalidOperationException("目标变量已有其他生产来源。");
        }
        foreach (var v in solution.GlobalVariables.Where(v => v.ProducerBinding?.NodeId == nodeId && v.ProducerBinding.WorkflowId == owners[0].Id))
            v.ProducerBinding = null;
        foreach (var (mapping, variable) in pending)
            variable.ProducerBinding = new() { WorkflowId = owners[0].Id, WorkflowName = owners[0].Name, NodeId = nodeId,
                NodeName = owners[0].Nodes.First(n => n.Id == nodeId).Name, ResultName = mapping.OutputKey, ResultDisplayName = variable.Name };
    }

    public async Task<PreparedCommunicationSend> PrepareAsync(CommunicationSendRequest request, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var solution = Solution;
        if (request.Inputs.Count == 0) throw new FormatException("请配置发送数据。");
        foreach (var input in request.Inputs) CommunicationSendTypes.Validate(input.Value, input.DataType);
        if (request.Kind == "Variable")
        {
            if (request.Inputs.Count != 1) throw new FormatException("变量提交单元必须为一项。");
            var input = request.Inputs[0];
            var variable = solution.GlobalVariables.FirstOrDefault(v => v.Id == input.TargetId) ?? throw new InvalidOperationException("目标变量不存在。");
            CommunicationSendTypes.RequireTarget(input.DataType, variable.Type, variable.Name);
            var value = CommunicationReceiveService.Normalize(input.Value);
            if (CommunicationReceiveService.ValueType(value) != variable.Type) throw new FormatException("发送值与变量类型不一致。");
            var producer = variable.ProducerBinding;
            var owners = solution.Workflows.Where(w => w.Nodes.Any(n => n.Id == request.NodeId)).ToArray();
            var workflowId = request.WorkflowId ?? (owners.Length == 1 ? owners[0].Id : null);
            if (producer == null || producer.NodeId != request.NodeId || producer.WorkflowId != workflowId || producer.ResultName != new CommunicationOutputField(input.FieldId, variable.Name, variable.Type).BindingKey)
                throw new InvalidOperationException("请重新确认发送变量绑定；发送节点不是目标变量生产者。");
            return new(variable.Name, value, ct =>
            {
                ct.ThrowIfCancellationRequested();
                if (!ReferenceEquals(solution, Solution) || !ReferenceEquals(producer, variable.ProducerBinding)) throw new InvalidOperationException("变量来源配置已变化。");
                // The tool output is the publication. Never overwrite persisted design initial values.
                return Task.FromResult(1);
            });
        }
        CommunicationSendEventDefinition definition;
        if (request.Kind == "Event")
        {
            definition = solution.CommunicationSendEvents.FirstOrDefault(e => e.EventId.ToString() == request.EventId)
                ?? throw new InvalidOperationException("发送事件不存在。");
            if (request.Inputs.Count != definition.Parameters.Count || request.Inputs.Select(i => i.FieldId).Distinct().Count() != request.Inputs.Count)
                throw new FormatException("发送事件参数发生变化，请重新确认绑定。");
        }
        else if (request.Kind == "Device")
        {
            definition = new() { DeviceId = request.DeviceId, TargetId = request.Inputs[0].TargetId,
                Separator = request.Options.Separator, Terminator = request.Options.Terminator };
        }
        else throw new NotSupportedException("队列发送尚未实现。");
        var originalDevice = solution.Communications.FirstOrDefault(d => d.Id == definition.DeviceId && d.IsEnabled)
            ?? throw new InvalidOperationException("目标设备不存在或已禁用。");
        var deviceLifetime = lifetimeReader?.Invoke(solution, originalDevice.Id) ?? CancellationToken.None;
        deviceLifetime.ThrowIfCancellationRequested();
        var signature = JsonSerializer.Serialize(originalDevice, WorkflowSerializationOptions.Default);
        var device = JsonSerializer.Deserialize<SunEyeVision.Workflow.Communication>(signature, WorkflowSerializationOptions.Default)!;
        var status = runtime.GetStatus(new(device.Id));
        if (!runtime.IsConnected(device.Id)) throw new InvalidOperationException("设备尚未连接。");
        var eventSignature = request.Kind == "Event" ? JsonSerializer.Serialize(definition, WorkflowSerializationOptions.Default) : null;
        definition = JsonSerializer.Deserialize<CommunicationSendEventDefinition>(JsonSerializer.Serialize(definition, WorkflowSerializationOptions.Default), WorkflowSerializationOptions.Default)!;
        CommunicationSendPlan plan;
        if (request.Kind == "Device" && device.IsRawProtocol)
        {
            var options = request.Options;
            if (options.Terminator is not ("" or "\r" or "\n" or "\r\n")) throw new FormatException("结束符配置无效。");
            byte[] bytes;
            if (request.Inputs.Any(i => i.Value is BinaryPayload))
            {
                if (request.Inputs.Count != 1 || request.Inputs[0].Value is not BinaryPayload payload || options.Separator != "" || options.Terminator != "")
                    throw new FormatException("完整字节报文只能单项发送，不追加分隔符或结束符。");
                bytes = payload.ToArray();
            }
            else if (options.Hex)
            {
                if (options.Separator != "") throw new FormatException("HEX发送不使用文本分隔符。");
                var chunks = request.Inputs.Select(i => i.Value is string text ? ProtocolValueCodec.ParseHex(text) : throw new FormatException("HEX输入必须为文本或完整字节载荷。"));
                bytes = chunks.SelectMany(c => c).Concat(Encoding.ASCII.GetBytes(options.Terminator)).ToArray();
            }
            else bytes = new UTF8Encoding(false, true).GetBytes(string.Join(options.Separator, request.Inputs.Select(i => Text(i.Value))) + options.Terminator);
            if (bytes.Length == 0) throw new FormatException("发送内容为空。");
            var message = new CommunicationSendMessage(Array.Empty<CommunicationSendValue>(), Array.Empty<CommunicationSendValue>(), "", bytes);
            plan = new(device.Id, "Raw", message, bytes);
        }
        else
        {
            if (request.Kind == "Device" && (request.Inputs.Count != 1 || request.Options != new CommunicationSendOptions()))
                throw new FormatException("原生目标每行一项，不使用HEX、分隔符或结束符。");
            if (device.IsOpcUa)
            {
                var point = device.OpcUaDataPoints.FirstOrDefault(p => p.Id == definition.TargetId) ?? throw new InvalidOperationException("数据点不存在。");
                await runtime.RefreshOpcUaPointAsync(device.Id, point.NodeId, token).ConfigureAwait(false);
            }
            var snapshot = new Dictionary<Guid, object?>();
            foreach (var input in request.Inputs)
            {
                string type; object value;
                if (request.Kind == "Event")
                {
                    type = definition.Parameters.FirstOrDefault(p => p.FieldId == input.FieldId)?.DataType ?? throw new FormatException("事件字段不存在。");
                    CommunicationSendTypes.RequireTarget(input.DataType, type, definition.Parameters.First(p => p.FieldId == input.FieldId).Name);
                    value = ConvertNative(input.Value, type);
                }
                else
                {
                    type = CommunicationSendTypes.Canonical(input.DataType);
                    if (device.IsModbus) type = type switch { "Integer" or "Int32" => "int", "Float" => "float", "String" => "string", _ => throw new FormatException($"Modbus目标 {device.ModbusAddressPoints.First(p => p.Id == definition.TargetId).Name} 当前编码支持 Integer／Int32、Float 或 String，不支持发送类型 {type}。") };
                    if (device.IsOpcUa)
                    {
                        var point = device.OpcUaDataPoints.First(p => p.Id == definition.TargetId);
                        if (!runtime.TryGetLatestOpcUaPoint(device.Id, point.NodeId, out var latest) || latest == null)
                            throw new InvalidOperationException($"无法确定目标 {point.Name} 的数据类型，请连接并刷新数据点。");
                        CommunicationSendTypes.RequireTarget(input.DataType, latest.BuiltInType.ToString(), point.Name);
                        type = latest.BuiltInType.ToString();
                    }
                    value = ConvertNative(input.Value, type);
                    definition.Parameters.Add(new() { FieldId = input.FieldId, Name = "data", DataType = type });
                }
                snapshot.Add(input.FieldId, value);
            }
            plan = _events.Prepare(definition, device, snapshot);
        }
        token.ThrowIfCancellationRequested();
        return new(device.Name, plan.Protocol == "OpcUa" ? CommunicationReceiveService.Normalize(plan.Scalar!) : new BinaryPayload(plan.PayloadBytes), async ct =>
        {
            ct.ThrowIfCancellationRequested();
            if (!ReferenceEquals(solution, Solution) || !runtime.IsConnected(device.Id) ||
                !ReferenceEquals(status, runtime.GetStatus(new(device.Id))) ||
                JsonSerializer.Serialize(solution.Communications.FirstOrDefault(d => d.Id == device.Id), WorkflowSerializationOptions.Default) != signature ||
                (eventSignature != null && JsonSerializer.Serialize(solution.CommunicationSendEvents.FirstOrDefault(e => e.EventId.ToString() == request.EventId), WorkflowSerializationOptions.Default) != eventSignature))
                throw new InvalidOperationException("发送前方案、连接或目标配置已变化，未提交。");
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, deviceLifetime);
            var count = await _events.SendAsync(plan, cancellation.Token, status).ConfigureAwait(false);
            if (count <= 0) throw new InvalidOperationException("没有可发送的连接目标。");
            return count;
        });
    }

    private static string Text(object value) => value switch
    {
        string s => s, long or int => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        bool b => b ? "true" : "false",
        double d when double.IsFinite(d) => d.ToString("R", CultureInfo.InvariantCulture),
        float f when float.IsFinite(f) => f.ToString("R", CultureInfo.InvariantCulture),
        _ => throw new FormatException("文本发送只支持String、Integer或Double，其他类型不自动转换。")
    };
    private static object ConvertNative(object value, string type)
    {
        var normalized = CommunicationReceiveService.Normalize(value);
        return type.ToLowerInvariant() switch
        {
            "string" when normalized is string => normalized,
            "boolean" or "bool" when normalized is bool => normalized,
            "int" or "int32" when normalized is long n => checked((int)n),
            "int16" when normalized is long n => checked((short)n),
            "uint16" when normalized is long n => checked((ushort)n),
            "uint32" when normalized is long n => checked((uint)n),
            "int64" when normalized is long => normalized,
            "float" or "float32" when normalized is double n && double.IsFinite(n) && n >= -float.MaxValue && n <= float.MaxValue => (float)n,
            "double" when normalized is double n && double.IsFinite(n) => n,
            _ => throw new FormatException("绑定值与目标声明类型不兼容，不自动解析文本或转换任意对象。")
        };
    }
}
