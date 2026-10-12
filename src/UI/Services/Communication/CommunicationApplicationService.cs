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
    private IReadOnlyList<CommunicationHeartbeatJob> _heartbeatPlans = Array.Empty<CommunicationHeartbeatJob>();
    private bool _heartbeatReady;
    private readonly OpcUaPointPolling _opcUaPolling;
    private readonly SemaphoreSlim _monitorGate = new(1, 1);
    private readonly ConcurrentDictionary<string, string> _monitorStatuses = new();
    public string GetOpcUaMonitorStatus(string pointId) => _monitorStatuses.GetValueOrDefault(pointId, "尚无实际监视信息；连接后可重新打开查看。");
    private readonly ConcurrentDictionary<Guid, string> _heartbeatStatuses = new();

    public CommunicationRuntimeComposition Composition { get; }
    public CommunicationHeartbeatService Heartbeats { get; }
    public CommunicationAutomationService Automation { get; }
    public SunEyeVision.Plugin.SDK.Models.Communication.CommunicationReceiveService Receives { get; } = new();

    public CommunicationApplicationService(SolutionManager solutions, CommunicationRuntimeComposition composition)
    {
        _solutions = solutions ?? throw new ArgumentNullException(nameof(solutions));
        Composition = composition ?? throw new ArgumentNullException(nameof(composition));
        Heartbeats = new CommunicationHeartbeatService(Composition.Runtime);
        Automation = new CommunicationAutomationService(Composition.Runtime);
        Receives.ActiveReader = ReadCurrentDeviceAsync;
        Receives.VariableCatalog = GetCurrentVariables;
        Receives.VariableReader = ReadCurrentVariables;
        _opcUaPolling = new(Composition.Runtime);
        Composition.Runtime.DataReceived += OnAutomationData;
        Composition.Runtime.OpcUaPointValueChanged += OnAutomationOpcUa;
        Composition.Polling.BatchReceived += OnAutomationModbus;
        Heartbeats.Runner.StatusChanged += OnHeartbeatStatus;
        _solutions.SolutionOpened += OnSolutionOpened;
        _solutions.SolutionClosed += OnSolutionClosed;
        Composition.Runtime.ConnectionClosed += OnConnectionClosed;
        Composition.Runtime.StatusChanged += OnReceiveStatus;
        if (_solutions.CurrentSolution is { } current)
            BeginSolutionSwitch(current);
    }

    public Task ReadyAsync()
    {
        lock (_gate) return _switchTask;
    }

    public void RefreshAutomationConfiguration(Solution solution)
    {
        lock (_gate)
            if (!_disposed && ReferenceEquals(solution, _activeSolution)) { ConfigureReceiveSources(solution); Automation.Refresh(solution); }
    }

    private void ConfigureReceiveSources(Solution? solution, bool reset = false)
    {
        var sources = new List<SunEyeVision.Plugin.SDK.Models.Communication.CommunicationReceiveSource>();
        foreach (var device in solution?.Communications.Where(d => d.IsEnabled) ?? Enumerable.Empty<ConfiguredCommunication>())
        {
            var signature = System.Text.Json.JsonSerializer.Serialize(device, WorkflowSerializationOptions.Default);
            if (device.IsRawProtocol) sources.Add(new(device.Id, "", device.Name, "Binary", signature, device.Name));
            else if (device.IsModbus)
                foreach (var point in device.ModbusAddressPoints.Where(p => p.AccessMode != "只写"))
                    sources.Add(new(device.Id, point.Id, $"{device.Name} / {point.Name}", "Binary", signature, device.Name, point.Name, true));
            else if (device.IsOpcUa)
                foreach (var point in device.OpcUaDataPoints)
                    sources.Add(new(device.Id, point.Id, $"{device.Name} / {point.Name}", "Native", signature, device.Name, point.Name, true));
        }
        Receives.Configure(sources, reset);
    }
    private void OnReceiveStatus(object? sender, SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationStatusSnapshot status)
    {
        if (status.State == SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationState.Connected)
            Receives.MarkConnected(status.Device.Value);
        if (status.State is SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationState.Disconnected
            or SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationState.Faulted)
            Receives.InvalidateDevice(status.Device.Value);
    }

    private async Task<(object Value, DateTimeOffset? SourceTime)> ReadCurrentDeviceAsync(
        SunEyeVision.Plugin.SDK.Models.Communication.CommunicationReceiveSource source, CancellationToken token)
    {
        ConfiguredCommunication device; int start = 0, count = 0; string nodeId = "";
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CommunicationApplicationService));
            device = _activeSolution?.Communications.FirstOrDefault(d => d.Id == source.DeviceId && d.IsEnabled)
                ?? throw new InvalidOperationException("设备来源不存在或已禁用。");
            if (System.Text.Json.JsonSerializer.Serialize(device, WorkflowSerializationOptions.Default) != source.ConfigurationKey)
                throw new InvalidOperationException("设备配置已变化，请重新选择来源。");
            if (device.IsModbus)
            {
                var point = device.ModbusAddressPoints.FirstOrDefault(p => p.Id == source.TargetId && p.AccessMode != "只写")
                    ?? throw new InvalidOperationException("地址不存在或不可读。");
                start = point.StartAddress; count = point.Count;
            }
            else if (device.IsOpcUa) nodeId = device.OpcUaDataPoints.FirstOrDefault(p => p.Id == source.TargetId)?.NodeId
                ?? throw new InvalidOperationException("数据点不存在。");
            else throw new NotSupportedException("来源不支持主动读取。");
        }
        if (device.IsModbus)
        {
            if (start < 0 || start > ushort.MaxValue || count < 1 || count > 125 || start + count > 65536)
                throw new InvalidOperationException("Modbus地址范围无效。");
            var words = await Composition.Runtime.ReadModbusHoldingRegistersAsync(device.Id, (ushort)start, (ushort)count, token).ConfigureAwait(false);
            if (words.Length != count) throw new InvalidOperationException("寄存器返回数量不匹配。");
            var bytes = new byte[words.Length * 2];
            for (var i = 0; i < words.Length; i++) { bytes[i * 2] = (byte)(words[i] >> 8); bytes[i * 2 + 1] = (byte)words[i]; }
            ValidateCurrentConfiguration();
            return (new SunEyeVision.Plugin.SDK.Models.Communication.BinaryPayload(bytes), null);
        }
        var value = await Composition.Runtime.ReadOpcUaValueAsync(device.Id, nodeId, token).ConfigureAwait(false);
        if (!Opc.Ua.StatusCode.IsGood(value.StatusCode) || value.Value == null) throw new InvalidOperationException("数据点质量无效或值为空。");
        ValidateCurrentConfiguration();
        return (SunEyeVision.Plugin.SDK.Models.Communication.CommunicationReceiveService.Normalize(value.Value),
            value.SourceTimestamp == DateTime.MinValue ? null : new DateTimeOffset(DateTime.SpecifyKind(value.SourceTimestamp, DateTimeKind.Utc)));
        void ValidateCurrentConfiguration()
        {
            token.ThrowIfCancellationRequested();
            lock (_gate)
                if (_disposed || _activeSolution?.Communications.FirstOrDefault(d => d.Id == source.DeviceId) != device || !device.IsEnabled
                    || System.Text.Json.JsonSerializer.Serialize(device, WorkflowSerializationOptions.Default) != source.ConfigurationKey)
                    throw new InvalidOperationException("读取期间来源配置已变化。");
        }
    }
    private IReadOnlyList<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource> GetCurrentVariables()
    {
        lock (_gate) lock (GlobalVariable.ValueGate)
            return (_activeSolution?.GlobalVariables ?? new()).Where(v => v.Type is "String" or "Integer" or "Double" or "Boolean" or "Binary")
                .Select(v => new SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource(v.Id, v.Name, v.Group, v.Type,
                    v.ProducerBinding != null, v.ProducerBinding?.NodeId ?? "", v.ProducerBinding?.ResultName ?? "", v.ProducerBinding?.WorkflowId ?? "")).ToArray();
    }
    private IReadOnlyDictionary<string, SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableValue> ReadCurrentVariables(IReadOnlyList<string> ids)
    {
        lock (_gate) lock (GlobalVariable.ValueGate)
        {
            if (_disposed || _activeSolution == null) throw new InvalidOperationException("当前方案不可用。");
            var result = new Dictionary<string, SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableValue>();
            foreach (var id in ids.Distinct())
            {
                var v = _activeSolution.GlobalVariables.FirstOrDefault(v => v.Id == id) ?? throw new InvalidOperationException("所选变量已删除。");
                if (v.ProducerBinding != null) throw new InvalidOperationException("所选变量有生产者，请在生产者成功后的方案运行中读取，不使用设计初值。");
                var value = SunEyeVision.Plugin.SDK.Models.Communication.CommunicationReceiveService.Normalize(v.Value ?? throw new InvalidOperationException("变量为空。"));
                if (SunEyeVision.Plugin.SDK.Models.Communication.CommunicationReceiveService.ValueType(value) != v.Type) throw new InvalidOperationException("变量声明与实际类型不匹配。");
                result[id] = new(new(v.Id, v.Name, v.Group, v.Type), value);
            }
            return result;
        }
    }

    private void OnAutomationData(object? sender, CommunicationDataReceivedEventArgs e)
    {
        lock (_gate) if (!_disposed)
        {
            var sample = Receives.Publish(e.CommunicationId, "", e.Timestamp, new SunEyeVision.Plugin.SDK.Models.Communication.BinaryPayload(e.Data));
            if (_heartbeatReady) Automation.ProcessRaw(e.CommunicationId, e.Data, e.Timestamp, sample);
        }
    }
    private void OnAutomationOpcUa(object? sender, OpcUaPointValueEventArgs e)
    {
        lock (_gate) if (!_disposed)
        {
            var point = _activeSolution?.Communications.FirstOrDefault(d => d.Id == e.CommunicationId)?.OpcUaDataPoints.FirstOrDefault(p => p.NodeId == e.NodeId);
            if (point == null) return;
            object? value = e.Value switch
            {
                string or bool => e.Value,
                byte or sbyte or short or ushort or int or uint or long => Convert.ToInt64(e.Value),
                ulong n when n <= long.MaxValue => (long)n,
                float f when float.IsFinite(f) => (double)f,
                double n when double.IsFinite(n) => n,
                _ => null
            };
            var good = value != null && e.IsSupportedScalar && Opc.Ua.StatusCode.IsGood(e.StatusCode) && string.IsNullOrEmpty(e.ErrorMessage);
            var sample = Receives.Publish(e.CommunicationId, point.Id, DateTimeOffset.UtcNow, value ?? "", good, sourceTimestamp: e.Timestamp);
            if (_heartbeatReady) Automation.ProcessOpcUa(e, sample);
        }
    }
    private void OnAutomationModbus(object? sender, ModbusPollBatch batch)
    {
        lock (_gate)
            if (!_disposed && Composition.Polling.IsCurrentBatch(batch))
                foreach (var sample in batch.Results)
                {
                    var point = _activeSolution?.Communications.FirstOrDefault(d => d.Id == sample.CommunicationId)?.ModbusAddressPoints.FirstOrDefault(p => p.Id == sample.AddressId);
                    if (point == null || point.StartAddress != sample.StartAddress || point.Count != sample.Count)
                    { Receives.InvalidateDevice(sample.CommunicationId); continue; }
                    var bytes = new byte[sample.Values.Length * 2];
                    for (var i = 0; i < sample.Values.Length; i++) { bytes[i * 2] = (byte)(sample.Values[i] >> 8); bytes[i * 2 + 1] = (byte)sample.Values[i]; }
                    var received = Receives.Publish(sample.CommunicationId, sample.AddressId, sample.Timestamp,
                        new SunEyeVision.Plugin.SDK.Models.Communication.BinaryPayload(bytes), sample.Values.Length == sample.Count);
                    if (_heartbeatReady) Automation.ProcessModbus(sample, received);
                }
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

    private void OnHeartbeatStatus(Guid id, string status)
    {
        var previous = _heartbeatStatuses.GetValueOrDefault(id);
        _heartbeatStatuses[id] = status;
        if (status != previous && status is not ("发送成功" or "等待连接或接收端"))
            LogWarning($"心跳 {id} 发送失败：{status}");
    }
    private void OnSolutionOpened(object? sender, Solution solution) => BeginSolutionSwitch(solution);
    private void OnSolutionClosed(object? sender, EventArgs e) => BeginSolutionSwitch(null);

    private void BeginSolutionSwitch(Solution? solution)
    {
        IReadOnlyList<CommunicationHeartbeatJob> heartbeatPlans;
        try { heartbeatPlans = Heartbeats.CreateJobs(solution); }
        catch (Exception ex) { LogWarning($"心跳配置无效，未启动：{ex.Message}"); heartbeatPlans = Array.Empty<CommunicationHeartbeatJob>(); }
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
            ConfigureReceiveSources(solution, reset: true);
            _heartbeatReady = false;
            Automation.Reset(solution);
            _heartbeatStatuses.Clear();
            _monitorStatuses.Clear();
            _heartbeatPlans = heartbeatPlans;
            Heartbeats.Runner.Update(Array.Empty<CommunicationHeartbeatJob>());
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

        await Heartbeats.StopAsync().ConfigureAwait(false);
        await Automation.DrainAsync().ConfigureAwait(false);
        await Composition.Polling.StopAllAsync().ConfigureAwait(false);
        await _monitorGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _opcUaPolling.StopAllAsync().ConfigureAwait(false);
            await Composition.Runtime.DisposeAsync().ConfigureAwait(false);
        }
        finally { _monitorGate.Release(); }
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
        lock (_gate)
            if (!token.IsCancellationRequested && !_disposed)
            {
                _heartbeatReady = true;
                Heartbeats.Runner.Update(_heartbeatPlans);
                Automation.SolutionLoaded();
            }
    }

    /// <summary>Called after configuration is captured into the shared solution; no file write.</summary>
    public void RefreshHeartbeatConfiguration(Solution solution)
    {
        if (!ReferenceEquals(solution, _solutions.CurrentSolution)) return;
        try
        {
            var plans = Heartbeats.CreateJobs(solution);
            lock (_gate)
            {
                if (_disposed || !ReferenceEquals(solution, _activeSolution)) return;
                _heartbeatPlans = plans;
                if (_heartbeatReady) Heartbeats.Runner.Update(plans);
            }
        }
        catch
        {
            lock (_gate)
            {
                _heartbeatPlans = Array.Empty<CommunicationHeartbeatJob>();
                if (!_disposed) Heartbeats.Runner.Update(_heartbeatPlans);
            }
            throw;
        }
    }

    private void OnConnectionClosed(object? sender, CommunicationConnectionClosedEventArgs e)
    {
        Automation.ResetDeviceHistory(e.CommunicationId);
        lock (_gate)
            foreach (var point in _activeSolution?.Communications.Find(d => d.Id == e.CommunicationId)?.OpcUaDataPoints ?? new())
                _monitorStatuses.TryRemove(point.Id, out _);
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
        CancellationToken lifetime;
        lock (_gate) { if (_disposed) return; lifetime = _switchCancellation?.Token ?? token; }
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime);
        await _monitorGate.WaitAsync(cancellation.Token).ConfigureAwait(false);
        try { await MonitorOpcUaPointCoreAsync(communicationId, nodeId, cancellation.Token).ConfigureAwait(false); }
        finally { _monitorGate.Release(); }
    }

    private async Task MonitorOpcUaPointCoreAsync(string communicationId, string nodeId, CancellationToken token)
    {
        if (!Composition.Runtime.IsConnected(communicationId)) return;
        OpcUaDataPoint? point;
        CancellationToken lifetime;
        lock (_gate)
        {
            if (_disposed) return;
            point = _activeSolution?.Communications.Find(d => d.Id == communicationId)?.OpcUaDataPoints.Find(p => p.NodeId == nodeId);
            lifetime = _switchCancellation?.Token ?? token;
        }
        if (point == null) return;
        var mode = point.MonitorMode;
        var interval = point.MonitorIntervalMs;
        try
        {
            if (interval is < 10 or > 600000 || mode is not ("变化订阅" or "周期读取"))
                throw new InvalidOperationException("OPC UA 监视模式或间隔无效，范围为 10～600000ms。");
            await UnmonitorOpcUaPointCoreAsync(communicationId, nodeId, token).ConfigureAwait(false);
            Automation.ResetDeviceHistory(communicationId, nodeId);
            await Composition.Runtime.RefreshOpcUaPointAsync(communicationId, nodeId, token).ConfigureAwait(false);
            if (mode == "周期读取")
            {
                await _opcUaPolling.StartAsync(communicationId, nodeId, interval, lifetime).ConfigureAwait(false);
                _monitorStatuses[point.Id] = $"周期读取间隔 {interval}ms；实际周期受读取耗时影响。";
            }
            else
            {
                await Composition.Runtime.SubscribeOpcUaValueAsync(communicationId, nodeId, interval, token).ConfigureAwait(false);
                var revised = await Composition.Runtime.GetOpcUaMonitoringIntervalsAsync(communicationId, nodeId, token).ConfigureAwait(false);
                _monitorStatuses[point.Id] = revised is { } actual
                    ? $"服务器接受：采样 {actual.Sampling}ms，发布 {actual.Publishing}ms。"
                    : "尚无服务器实际订阅间隔。";
            }
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
        await _monitorGate.WaitAsync(token).ConfigureAwait(false);
        try { await UnmonitorOpcUaPointCoreAsync(communicationId, nodeId, token).ConfigureAwait(false); }
        finally { _monitorGate.Release(); }
    }

    private async Task UnmonitorOpcUaPointCoreAsync(string communicationId, string nodeId, CancellationToken token)
    {
        await _opcUaPolling.StopAsync(communicationId, nodeId).ConfigureAwait(false);
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
            Heartbeats.Runner.Update(Array.Empty<CommunicationHeartbeatJob>());
            Automation.Reset(null);
            Receives.Configure(Array.Empty<SunEyeVision.Plugin.SDK.Models.Communication.CommunicationReceiveSource>(), reset: true);
            Composition.Runtime.DataReceived -= OnAutomationData;
            Composition.Runtime.OpcUaPointValueChanged -= OnAutomationOpcUa;
            Composition.Polling.BatchReceived -= OnAutomationModbus;
            _solutions.SolutionOpened -= OnSolutionOpened;
            _solutions.SolutionClosed -= OnSolutionClosed;
            Composition.Runtime.ConnectionClosed -= OnConnectionClosed;
            Composition.Runtime.StatusChanged -= OnReceiveStatus;
            _switchCancellation?.Cancel();
            pending = _switchTask;
            pendingRetries = _reconnectTasks.ToArray();
        }
        try { await pending.ConfigureAwait(false); }
        catch (Exception ex) { LogWarning($"关闭通讯服务时取消方案切换：{ex.Message}"); }
        try { await Task.WhenAll(pendingRetries).ConfigureAwait(false); }
        catch (Exception ex) { LogWarning($"关闭通讯服务时取消重连：{ex.Message}"); }
        await Heartbeats.DisposeAsync().ConfigureAwait(false);
        await _opcUaPolling.StopAllAsync().ConfigureAwait(false);
        await Automation.DisposeAsync().ConfigureAwait(false);
        Heartbeats.Runner.StatusChanged -= OnHeartbeatStatus;
        _switchCancellation?.Dispose();
    }
}
