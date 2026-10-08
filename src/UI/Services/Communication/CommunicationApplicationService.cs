using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.Workflow;
using ConfiguredCommunication = SunEyeVision.Workflow.Communication;

namespace SunEyeVision.UI.Services.Communication;

/// <summary>
/// Owns the solution-level communication lifecycle. It does not access WPF controls.
/// </summary>
public sealed class CommunicationApplicationService : IAsyncDisposable
{
    private sealed record ConnectionPlan(CommunicationDeviceSnapshot Configuration, int PollInterval,
        ModbusPollAddress[] Addresses, string[] OpcUaNodeIds);

    private readonly SolutionManager _solutions;
    private readonly object _gate = new();
    private Task _switchTask = Task.CompletedTask;
    private CancellationTokenSource? _switchCancellation;
    private Solution? _activeSolution;
    private readonly ConcurrentDictionary<string, byte> _reconnecting = new(StringComparer.Ordinal);
    private readonly List<Task> _reconnectTasks = new();
    private bool _disposed;

    public CommunicationRuntimeComposition Composition { get; }

    public CommunicationApplicationService(SolutionManager solutions, CommunicationRuntimeComposition composition)
    {
        _solutions = solutions ?? throw new ArgumentNullException(nameof(solutions));
        Composition = composition ?? throw new ArgumentNullException(nameof(composition));
        _solutions.SolutionOpened += OnSolutionOpened;
        _solutions.SolutionClosed += OnSolutionClosed;
        Composition.Runtime.ConnectionClosed += OnConnectionClosed;
        if (_solutions.CurrentSolution is { } current)
            BeginSolutionSwitch(current);
    }

    public Task ReadyAsync()
    {
        lock (_gate) return _switchTask;
    }

    /// <summary>Host entry point for a caller's typed execution snapshot.</summary>
    public Task<int> SendEventAsync(Guid eventId, IReadOnlyDictionary<Guid, object?> parameterSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameterSnapshot);
        var solution = _solutions.CurrentSolution ?? throw new InvalidOperationException("未打开方案。");
        var definition = solution.CommunicationSendEvents.FirstOrDefault(item => item.EventId == eventId)
            ?? throw new InvalidOperationException("发送事件不存在或已被删除。");
        var device = solution.Communications.FirstOrDefault(item => item.Id == definition.DeviceId)
            ?? throw new InvalidOperationException("发送事件的绑定设备不存在。");
        var service = new CommunicationSendEventService(Composition.Runtime);
        var plan = service.Prepare(definition, device, parameterSnapshot);
        return service.SendAsync(plan, cancellationToken);
    }

    private void OnSolutionOpened(object? sender, Solution solution) => BeginSolutionSwitch(solution);
    private void OnSolutionClosed(object? sender, EventArgs e) => BeginSolutionSwitch(null);

    private void BeginSolutionSwitch(Solution? solution)
    {
        var plans = solution?.Communications.Where(item => item.IsEnabled)
            .Select(item => new ConnectionPlan(CommunicationDeviceSnapshotFactory.Create(item), item.PollInterval,
                item.ModbusAddressPoints.Where(point => point.PollEnabled && point.AccessMode != "只写")
                    .Select(point => new ModbusPollAddress(point.Id, point.StartAddress, point.Count, point.TimeoutMs))
                    .ToArray(),
                item.IsOpcUa ? (item.OpcUaDataPoints ?? new()).Select(point => point.NodeId).ToArray() : Array.Empty<string>()))
            .ToArray() ?? Array.Empty<ConnectionPlan>();
        lock (_gate)
        {
            if (_disposed) return;
            _switchCancellation?.Cancel();
            var pendingRetries = _reconnectTasks.ToArray();
            _reconnectTasks.RemoveAll(task => task.IsCompleted);
            _activeSolution = solution;
            _switchCancellation = new CancellationTokenSource();
            var previous = _switchTask;
            var token = _switchCancellation.Token;
            _switchTask = Task.Run(() => SwitchAsync(previous, pendingRetries, plans, token));
        }
    }

    private async Task SwitchAsync(Task previous, Task[] pendingRetries,
        ConnectionPlan[] plans, CancellationToken token)
    {
        try { await previous.ConfigureAwait(false); }
        catch (Exception ex) { LogWarning($"上一次通讯方案切换未完成：{ex.Message}"); }
        try { await Task.WhenAll(pendingRetries).ConfigureAwait(false); }
        catch (Exception ex) { LogWarning($"取消旧方案重连失败：{ex.Message}"); }

        await Composition.Polling.StopAllAsync().ConfigureAwait(false);
        await Composition.Runtime.DisposeAsync().ConfigureAwait(false);
        if (token.IsCancellationRequested) return;

        foreach (var plan in plans)
        {
            if (token.IsCancellationRequested) return;
            try
            {
                await Composition.Runtime.ConnectAsync(plan.Configuration, token)
                    .ConfigureAwait(false);
                if (plan.Configuration.IsModbus)
                    Composition.Polling.Start(plan.Configuration.Id, plan.PollInterval, plan.Addresses);
                if (plan.Configuration.ProtocolKey == "opcua")
                    await MonitorOpcUaPointsAsync(plan.Configuration.Id, plan.OpcUaNodeIds, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception ex) { LogWarning($"自动连接通讯设备“{plan.Configuration.Name}”失败：{ex.Message}"); }
        }
    }

    private void OnConnectionClosed(object? sender, CommunicationConnectionClosedEventArgs e)
    {
        ConfiguredCommunication? device;
        CancellationToken token;
        lock (_gate)
        {
            device = _activeSolution?.Communications.FirstOrDefault(item => item.Id == e.CommunicationId);
            token = _switchCancellation?.Token ?? CancellationToken.None;
            if (_disposed || device is null || !device.IsEnabled || !device.AutoReconnect ||
                token.IsCancellationRequested || !_reconnecting.TryAdd(device.Id, 0)) return;
            _reconnectTasks.RemoveAll(task => task.IsCompleted);
            _reconnectTasks.Add(ReconnectAsync(device, token));
        }
    }

    private async Task ReconnectAsync(ConfiguredCommunication device, CancellationToken token)
    {
        try
        {
            var delaySeconds = 1;
            while (!token.IsCancellationRequested && device.IsEnabled && device.AutoReconnect)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token).ConfigureAwait(false);
                try
                {
                    await Composition.Runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(device), token)
                        .ConfigureAwait(false);
                    if (device.IsModbus)
                        Composition.Polling.Start(device.Id, device.PollInterval,
                            device.ModbusAddressPoints.Where(point => point.PollEnabled && point.AccessMode != "只写")
                                .Select(point => new ModbusPollAddress(point.Id, point.StartAddress, point.Count, point.TimeoutMs)));
                    if (device.IsOpcUa)
                        await MonitorOpcUaPointsAsync(device.Id,
                            (device.OpcUaDataPoints ?? new()).Select(point => point.NodeId), token).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    LogWarning($"通讯设备“{device.Name}”重连失败，{Math.Min(delaySeconds * 2, 30)} 秒后重试：{ex.Message}");
                    delaySeconds = Math.Min(delaySeconds * 2, 30);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally { _reconnecting.TryRemove(device.Id, out _); }
    }

    private static void LogWarning(string message) =>
        VisionLogger.Instance.Log(LogLevel.Warning, message, "CommunicationApplication");

    public async Task MonitorOpcUaPointAsync(string communicationId, string nodeId, CancellationToken token = default)
    {
        if (!Composition.Runtime.IsConnected(communicationId)) return;
        try
        {
            await Composition.Runtime.RefreshOpcUaPointAsync(communicationId, nodeId, token).ConfigureAwait(false);
            await Composition.Runtime.SubscribeOpcUaValueAsync(communicationId, nodeId, 100, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            Composition.Runtime.ReportOpcUaPointError(communicationId, nodeId, ex.Message);
            LogWarning($"OPC UA 数据点 {nodeId} 监视失败：{ex.Message}");
        }
    }

    public async Task UnmonitorOpcUaPointAsync(string communicationId, string nodeId, CancellationToken token = default)
    {
        if (Composition.Runtime.IsConnected(communicationId))
            await Composition.Runtime.UnsubscribeOpcUaValueAsync(communicationId, nodeId, token).ConfigureAwait(false);
    }

    private async Task MonitorOpcUaPointsAsync(string communicationId, IEnumerable<string> nodeIds, CancellationToken token)
    {
        foreach (var nodeId in nodeIds)
        {
            token.ThrowIfCancellationRequested();
            await MonitorOpcUaPointAsync(communicationId, nodeId, token).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task pending;
        Task[] pendingRetries;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _solutions.SolutionOpened -= OnSolutionOpened;
            _solutions.SolutionClosed -= OnSolutionClosed;
            Composition.Runtime.ConnectionClosed -= OnConnectionClosed;
            _switchCancellation?.Cancel();
            pending = _switchTask;
            pendingRetries = _reconnectTasks.ToArray();
        }
        try { await pending.ConfigureAwait(false); }
        catch (Exception ex) { LogWarning($"关闭通讯服务时取消方案切换：{ex.Message}"); }
        try { await Task.WhenAll(pendingRetries).ConfigureAwait(false); }
        catch (Exception ex) { LogWarning($"关闭通讯服务时取消重连：{ex.Message}"); }
        _switchCancellation?.Dispose();
    }
}
