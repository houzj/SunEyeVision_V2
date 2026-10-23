using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
public sealed partial class CommunicationApplicationService : IAsyncDisposable
{
    private sealed record ConnectionPlan(CommunicationDeviceSnapshot Configuration, int PollInterval,
        ModbusPollAddress[] Addresses, string[] OpcUaNodeIds);

    private readonly SolutionManager _solutions;
    private readonly object _gate = new();
    private Task _switchTask = Task.CompletedTask;
    private CancellationTokenSource? _switchCancellation;
    private Solution? _activeSolution;
    private volatile bool _connectionsReady;
    private sealed class DeviceLifetime
    {
        public DeviceLifetime(ConfiguredCommunication device, CancellationToken solutionToken)
        {
            Device = device;
            Cancellation = CancellationTokenSource.CreateLinkedTokenSource(solutionToken);
            Token = Cancellation.Token;
        }
        public ConfiguredCommunication Device { get; }
        public CancellationTokenSource Cancellation { get; }
        public CancellationToken Token { get; }
    }
    private readonly Dictionary<string, DeviceLifetime> _deviceLifetimes = new(StringComparer.Ordinal);

    private DeviceLifetime GetDeviceLifetimeLocked(ConfiguredCommunication device)
    {
        if (_deviceLifetimes.TryGetValue(device.Id, out var current) &&
            ReferenceEquals(current.Device, device) && !current.Cancellation.IsCancellationRequested) return current;
        if (current != null) CancelDeviceLifetimeLocked(device.Id);
        var lifetime = new DeviceLifetime(device, _switchCancellation?.Token ?? CancellationToken.None);
        _deviceLifetimes[device.Id] = lifetime;
        return lifetime;
    }

    private void CancelDeviceLifetimeLocked(string id)
    {
        if (_deviceLifetimes.Remove(id, out var lifetime))
        {
            lifetime.Cancellation.Cancel();
            lifetime.Cancellation.Dispose();
        }
        if (_reconnecting.TryGetValue(id, out var retry)) retry.Cancellation.Cancel();
    }

    private bool IsCurrentDeviceLocked(ConfiguredCommunication device, CancellationToken solutionToken) =>
        !_disposed && !solutionToken.IsCancellationRequested &&
        (_switchCancellation?.Token ?? CancellationToken.None) == solutionToken &&
        ReferenceEquals(_activeSolution?.Communications.Find(d => d.Id == device.Id), device);
    private sealed class ReconnectAttempt(CancellationToken lifetime)
    {
        public Guid ConnectionOwner { get; } = Guid.NewGuid();
        public CancellationTokenSource Cancellation { get; } = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        public Task Task { get; set; } = Task.CompletedTask;
        public bool ConnectionLostAgain { get; set; }
        public bool Waiting { get; set; }
        public long RetryDueTimestamp { get; set; }
    }
    private readonly Dictionary<string, ReconnectAttempt> _reconnecting = new(StringComparer.Ordinal);
    private const int MaxReconnectFailures = 5;
    // Runtime-only budget: reopening the manager must not restart an exhausted cycle.
    private readonly Dictionary<string, int> _reconnectFailures = new(StringComparer.Ordinal);
    private readonly HashSet<string> _manualDisconnects = new(StringComparer.Ordinal);
    private readonly HashSet<string> _connectionIntents = new(StringComparer.Ordinal);
    private readonly HashSet<ConfiguredCommunication> _observedDevices = new();
    private readonly HashSet<ConfiguredCommunication> _retiredDevices = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _deviceGates = new(StringComparer.Ordinal);
    private sealed class ConnectionPolicyUpdate(ConfiguredCommunication device, CancellationToken lifetime)
    {
        public ConfiguredCommunication Device { get; } = device;
        public CancellationToken Lifetime { get; } = lifetime;
        public long Revision { get; set; }
        // Coalescing must retain a disable even when the final configuration is enabled.
        public bool RequiresDisconnect { get; set; }
    }
    private readonly Dictionary<string, ConnectionPolicyUpdate> _policyUpdates = new(StringComparer.Ordinal);
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
    public WorkflowCommunicationSendService Sends { get; }
    public OutputBindingChangeService OutputBindings { get; } = new();

    public CommunicationApplicationService(SolutionManager solutions, CommunicationRuntimeComposition composition)
    {
        _solutions = solutions ?? throw new ArgumentNullException(nameof(solutions));
        Composition = composition ?? throw new ArgumentNullException(nameof(composition));
        Sends = new(() => { lock (_gate) return _disposed ? null : _activeSolution; },
            Composition.Runtime, GetDeviceOperationToken);
        Heartbeats = new CommunicationHeartbeatService(Composition.Runtime);
        Automation = new CommunicationAutomationService(Composition.Runtime);
        Automation.DeviceLifetimeReader = id =>
        {
            lock (_gate)
                return _activeSolution is { } solution ? GetDeviceOperationToken(solution, id) : new CancellationToken(true);
        };
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
        _solutions.SolutionSaving += OnSolutionSaving;
        Composition.Runtime.ConnectionClosed += OnConnectionClosed;
        Composition.Runtime.StatusChanged += OnReceiveStatus;
        if (_solutions.CurrentSolution is { } current)
            BeginSolutionSwitch(current);
    }

    public Task ReadyAsync()
    {
        lock (_gate) return _switchTask;
    }

    public async Task VerifyWorkspaceReadyAsync(Solution expected)
    {
        await ReadyAsync().WaitAsync(TimeSpan.FromSeconds(30));
        lock (_gate)
        {
            if (!ReferenceEquals(expected, _activeSolution)) throw new InvalidOperationException("通讯初始化所属方案已变化。");
            if (!_connectionsReady || !_connectionErrors.IsEmpty)
                throw new InvalidOperationException("方案已提交，部分通讯初始化未完成：" + string.Join("；", _connectionErrors.Values));
        }
    }

    public void RefreshAutomationConfiguration(Solution solution)
    {
        lock (_gate)
            if (!_disposed && ReferenceEquals(solution, _activeSolution))
            {
                ObserveDevicesLocked(solution);
                ConfigureReceiveSources(solution);
                Automation.Refresh(solution);
            }
    }

    private void ConfigureReceiveSources(Solution? solution, bool reset = false)
    {
        var sources = new List<SunEyeVision.Plugin.SDK.Models.Communication.CommunicationReceiveSource>();
        foreach (var device in solution?.Communications.Where(d => d.IsEnabled) ?? Enumerable.Empty<ConfiguredCommunication>())
        {
            var signature = ReceiveConfigurationKey(device);
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
    private static string ReceiveConfigurationKey(ConfiguredCommunication device)
    {
        var config = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(device, WorkflowSerializationOptions.Default))!.AsObject();
        // Labels do not change the current connection or the meaning of received bytes.
        config.Remove(nameof(ConfiguredCommunication.Name));
        config.Remove(nameof(ConfiguredCommunication.Description));
        return config.ToJsonString();
    }
    private void OnReceiveStatus(object? sender, SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationStatusSnapshot status)
    {
        // During teardown these events still belong to the old runtime generation.
        // Runtime publishes synchronously under its state gate. Do not acquire the
        // application gate here: policy cancellation may hold it while invalidating
        // that runtime. Teardown drains the runtime before enabling the next epoch.
        if (!_connectionsReady) return;
        if (status.State == SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationState.Connected)
            _connectionErrors.TryRemove(status.Device.Value, out _);
        else if (!string.IsNullOrWhiteSpace(status.LastError?.Message))
            _connectionErrors[status.Device.Value] = status.LastError.Message;
        if (status.State == SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationState.Connected)
            Receives.MarkConnected(status.Device.Value);
        if (status.State is SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationState.Disconnected
            or SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationState.Faulted)
        {
            Composition.Polling.Stop(status.Device.Value);
            Receives.InvalidateDevice(status.Device.Value);
        }
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
            if (ReceiveConfigurationKey(device) != source.ConfigurationKey)
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
                    || ReceiveConfigurationKey(device) != source.ConfigurationKey)
                    throw new InvalidOperationException("读取期间来源配置已变化。");
        }
    }
    private IReadOnlyList<SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource> GetCurrentVariables()
    {
        lock (_gate) lock (GlobalVariable.ValueGate)
            return (_activeSolution?.GlobalVariables ?? new()).Where(v => v.Type is "String" or "Integer" or "Double" or "Boolean" or "Binary")
                .Select(v => new SunEyeVision.Plugin.SDK.Models.Communication.ReceiveVariableSource(v.Id, v.Name, v.Group, v.Type,
                    v.ProducerBinding != null || !string.IsNullOrEmpty(v.RebindingReason) || !string.IsNullOrEmpty(v.BindingValidationPending), v.ProducerBinding?.NodeId ?? "", v.ProducerBinding?.ResultName ?? "", v.ProducerBinding?.WorkflowId ?? "")).ToArray();
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
                if (!string.IsNullOrEmpty(v.RebindingReason)) throw new InvalidOperationException(v.RebindingReason);
                if (!string.IsNullOrEmpty(v.BindingValidationPending)) throw new InvalidOperationException(v.BindingValidationPending);
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
    public async Task<int> SendEventAsync(Guid eventId, IReadOnlyDictionary<Guid, object?> parameterSnapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameterSnapshot);
        var solution = _solutions.CurrentSolution ?? throw new InvalidOperationException("未打开方案。");
        var definition = solution.CommunicationSendEvents.FirstOrDefault(item => item.EventId == eventId)
            ?? throw new InvalidOperationException("发送事件不存在或已被删除。");
        var device = solution.Communications.FirstOrDefault(item => item.Id == definition.DeviceId)
            ?? throw new InvalidOperationException("发送事件的绑定设备不存在。");
        var service = new CommunicationSendEventService(Composition.Runtime);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, GetDeviceOperationToken(solution, device.Id));
        var status = Composition.Runtime.GetStatus(new(device.Id));
        var plan = service.Prepare(definition, device, parameterSnapshot);
        return await service.SendAsync(plan, cancellation.Token, status).ConfigureAwait(false);
    }

    internal CancellationToken GetDeviceOperationToken(Solution expectedSolution, string deviceId)
    {
        lock (_gate)
        {
            var device = _activeSolution?.Communications.Find(d => d.Id == deviceId);
            if (_disposed || !_connectionsReady || !ReferenceEquals(expectedSolution, _activeSolution) ||
                device?.IsEnabled != true || _retiredDevices.Contains(device) || _manualDisconnects.Contains(deviceId))
                return new CancellationToken(true);
            return GetDeviceLifetimeLocked(device).Token;
        }
    }

    private void OnHeartbeatStatus(Guid id, string status)
    {
        var previous = _heartbeatStatuses.GetValueOrDefault(id);
        _heartbeatStatuses[id] = status;
        if (status != previous && status is not ("发送成功" or "等待连接或接收端"))
            LogWarning($"心跳 {id} 发送失败：{status}");
    }
    private void OnSolutionOpened(object? sender, Solution solution) => BeginSolutionSwitch(solution);
    private void OnSolutionSaving(object? sender, Solution solution)
    {
        if (ReferenceEquals(solution, _activeSolution)) OutputBindings.ReconcileAll();
        else { var validation = new OutputBindingChangeService(); validation.Reset(solution); validation.ReconcileAll(); }
    }
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
            _connectionsReady = false;
            var previousCancellation = _switchCancellation;
            previousCancellation?.Cancel();
            foreach (var id in _deviceLifetimes.Keys.ToArray()) CancelDeviceLifetimeLocked(id);
            _manualDisconnects.Clear();
            _retiredDevices.Clear();
            _policyUpdates.Clear();
            _connectionIntents.Clear();
            foreach (var plan in plans) _connectionIntents.Add(plan.Configuration.Id);
            _connectionErrors.Clear();
            _reconnectFailures.Clear();
            var pendingRetries = _reconnectTasks.ToArray();
            _reconnectTasks.RemoveAll(task => task.IsCompleted);
            _activeSolution = solution;
            ObserveDevicesLocked(solution);
            OutputBindings.Reset(solution);
            OutputBindings.ReconcileAll();
            ConfigureReceiveSources(solution, reset: true);
            _heartbeatReady = false;
            Automation.Reset(solution);
            _heartbeatStatuses.Clear();
            _monitorStatuses.Clear();
            _heartbeatPlans = heartbeatPlans;
            Heartbeats.Runner.Update(Array.Empty<CommunicationHeartbeatJob>());
            _switchCancellation = new CancellationTokenSource();
            previousCancellation?.Dispose();
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
        lock (_gate)
        {
            if (_disposed || token.IsCancellationRequested || _switchCancellation?.Token != token) return;
            _connectionsReady = true;
            _connectionErrors.Clear();
        }

        foreach (var plan in plans)
        {
            if (token.IsCancellationRequested) return;
            DeviceLifetime lifetime;
            lock (_gate)
            {
                var device = _activeSolution?.Communications.Find(d => d.Id == plan.Configuration.Id);
                if (_switchCancellation?.Token != token || device?.IsEnabled != true ||
                    !_connectionIntents.Contains(plan.Configuration.Id) || _manualDisconnects.Contains(plan.Configuration.Id)) continue;
                lifetime = GetDeviceLifetimeLocked(device);
            }
            var deviceToken = lifetime.Token;
            try
            {
                await Composition.Runtime.ConnectAsync(plan.Configuration, deviceToken)
                    .ConfigureAwait(false);
                lock (_gate)
                {
                    if (!IsCurrentDeviceLocked(lifetime.Device, token) || deviceToken.IsCancellationRequested || !lifetime.Device.IsEnabled) continue;
                    _reconnectFailures.Remove(lifetime.Device.Id);
                    if (plan.Configuration.IsModbus)
                        Composition.Polling.Start(plan.Configuration.Id, plan.PollInterval, plan.Addresses);
                }
                if (plan.Configuration.ProtocolKey == "opcua")
                    await MonitorOpcUaPointsAsync(plan.Configuration.Id, plan.OpcUaNodeIds, deviceToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (OperationCanceledException) when (deviceToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                LogWarning($"自动连接通讯设备“{plan.Configuration.Name}”失败：{ex.Message}");
                if (!token.IsCancellationRequested)
                    OnConnectionClosed(this, new CommunicationConnectionClosedEventArgs(plan.Configuration.Id));
            }
        }
        lock (_gate)
            if (!token.IsCancellationRequested && !_disposed)
            {
                // Policy changes during teardown cannot schedule retries until ready.
                // Include devices enabled after the startup plan was captured.
                foreach (var device in _activeSolution?.Communications.ToArray() ?? Array.Empty<ConfiguredCommunication>())
                    if (device.IsEnabled && device.AutoReconnect && !_retiredDevices.Contains(device) &&
                        _connectionIntents.Contains(device.Id) && !_manualDisconnects.Contains(device.Id))
                        OnConnectionClosed(this, new CommunicationConnectionClosedEventArgs(device.Id));
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
        CancellationToken eventLifetime;
        lock (_gate)
        {
            if (_disposed || !_connectionsReady) return;
            eventLifetime = _switchCancellation?.Token ?? CancellationToken.None;
        }
        if (Composition.Runtime.IsConnected(e.CommunicationId)) return;
        var state = Composition.Runtime.GetStatus(new SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationDeviceRef(e.CommunicationId)).State;
        if (state is SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationState.Connecting or
            SunEyeVision.Plugin.SDK.Devices.Communication.CommunicationState.Disconnecting) return;
        ConfiguredCommunication? device;
        CancellationToken token;
        lock (_gate)
        {
            if (_disposed || !_connectionsReady || eventLifetime.IsCancellationRequested ||
                _switchCancellation?.Token != eventLifetime ||
                (e.RuntimeStatus != null && !Composition.Runtime.IsCurrentStatus(e.RuntimeStatus))) return;
            Composition.Polling.Stop(e.CommunicationId);
            Automation.ResetDeviceHistory(e.CommunicationId);
            foreach (var point in _activeSolution?.Communications.Find(d => d.Id == e.CommunicationId)?.OpcUaDataPoints ?? new())
                _monitorStatuses.TryRemove(point.Id, out _);
            device = _activeSolution?.Communications.FirstOrDefault(item => item.Id == e.CommunicationId);
            token = _switchCancellation?.Token ?? CancellationToken.None;
            if (_disposed || !_connectionsReady || device is null || _retiredDevices.Contains(device) || !device.IsEnabled || !device.AutoReconnect ||
                token.IsCancellationRequested || !_connectionIntents.Contains(device.Id) || _manualDisconnects.Contains(device.Id)) return;
            if (_reconnectFailures.GetValueOrDefault(device.Id) >= MaxReconnectFailures) return;
            if (_reconnecting.TryGetValue(device.Id, out var existing))
            {
                existing.ConnectionLostAgain = true;
                return;
            }
            var attempt = new ReconnectAttempt(GetDeviceLifetimeLocked(device).Token);
            _reconnecting.Add(device.Id, attempt);
            _reconnectTasks.RemoveAll(task => task.IsCompleted);
            attempt.Task = ReconnectAsync(device, attempt);
            _reconnectTasks.Add(attempt.Task);
        }
    }

    private void ObserveDevicesLocked(Solution? solution)
    {
        var current = (solution?.Communications ?? new()).ToHashSet();
        foreach (var removed in _observedDevices.Except(current).ToArray())
        {
            CancelDeviceLifetimeLocked(removed.Id);
            _policyUpdates.Remove(removed.Id);
            _reconnectFailures.Remove(removed.Id);
            removed.PropertyChanged -= ConnectionPolicyChanged;
            _observedDevices.Remove(removed);
            _retiredDevices.Remove(removed);
            if (!current.Any(device => device.Id == removed.Id)) _connectionIntents.Remove(removed.Id);
        }
        foreach (var added in current.Except(_observedDevices))
        {
            added.PropertyChanged += ConnectionPolicyChanged;
            _observedDevices.Add(added);
        }
    }

    /// <summary>Reconcile a restored configuration without changing its persisted value.</summary>
    public void RefreshDeviceConnectionPolicy(string deviceId)
    {
        lock (_gate)
        {
            if (_disposed) return;
            var device = _activeSolution?.Communications.Find(d => d.Id == deviceId);
            if (device != null) ConnectionPolicyChanged(device,
                new PropertyChangedEventArgs(nameof(ConfiguredCommunication.AutoReconnect)));
        }
    }

    private void ConnectionPolicyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not ConfiguredCommunication device ||
            args.PropertyName is not (nameof(ConfiguredCommunication.AutoReconnect) or nameof(ConfiguredCommunication.IsEnabled))) return;
        lock (_gate)
        {
            if (_disposed || !_observedDevices.Contains(device) || _retiredDevices.Contains(device)) return;
            if (args.PropertyName == nameof(ConfiguredCommunication.IsEnabled)) ConfigureReceiveSources(_activeSolution);
            if (!device.IsEnabled)
            {
                // Do not let startup completion or an unrelated close event schedule
                // a replacement until this disable has drained, even after re-enable.
                _connectionIntents.Remove(device.Id);
                CancelDeviceLifetimeLocked(device.Id);
            }
            if ((!device.IsEnabled || !device.AutoReconnect) && _reconnecting.TryGetValue(device.Id, out var retry))
                retry.Cancellation.Cancel();
            if (args.PropertyName == nameof(ConfiguredCommunication.IsEnabled) && _activeSolution is { } solution)
            {
                Automation.Refresh(solution);
                RefreshHeartbeatConfiguration(solution);
            }
            var lifetime = _switchCancellation?.Token ?? CancellationToken.None;
            if (_policyUpdates.TryGetValue(device.Id, out var pending) &&
                ReferenceEquals(pending.Device, device) && pending.Lifetime == lifetime)
            {
                pending.RequiresDisconnect |= !device.IsEnabled;
                pending.Revision++;
                return;
            }
            var update = new ConnectionPolicyUpdate(device, lifetime) { RequiresDisconnect = !device.IsEnabled };
            _policyUpdates[device.Id] = update;
            _reconnectTasks.RemoveAll(task => task.IsCompleted);
            _reconnectTasks.Add(Task.Run(() => DrainConnectionPolicyUpdatesAsync(update)));
        }
    }

    private bool IsCurrentPolicyLocked(ConnectionPolicyUpdate update, long revision) =>
        !_disposed && !_retiredDevices.Contains(update.Device) && !update.Lifetime.IsCancellationRequested &&
        _policyUpdates.TryGetValue(update.Device.Id, out var current) && ReferenceEquals(current, update) &&
        current.Revision == revision && _activeSolution?.Communications.Find(d => d.Id == update.Device.Id) == update.Device;

    private async Task DrainConnectionPolicyUpdatesAsync(ConnectionPolicyUpdate update)
    {
        try
        {
            while (true)
            {
                long revision;
                lock (_gate)
                {
                    revision = update.Revision;
                    if (!IsCurrentPolicyLocked(update, revision)) return;
                }
                await ApplyConnectionPolicyAsync(update, revision).ConfigureAwait(false);
                lock (_gate)
                {
                    if (!_policyUpdates.TryGetValue(update.Device.Id, out var current) || !ReferenceEquals(current, update)) return;
                    if (update.Revision != revision) continue;
                    _policyUpdates.Remove(update.Device.Id);
                    return;
                }
            }
        }
        finally
        {
            lock (_gate)
                if (_policyUpdates.TryGetValue(update.Device.Id, out var current) && ReferenceEquals(current, update))
                    _policyUpdates.Remove(update.Device.Id);
        }
    }

    private async Task ApplyConnectionPolicyAsync(ConnectionPolicyUpdate update, long revision)
    {
        var device = update.Device;
        var lifetime = update.Lifetime;
        var deviceGate = _deviceGates.GetOrAdd(device.Id, _ => new SemaphoreSlim(1, 1));
        try
        {
            // The old solution switch is allowed to drain these tasks: do not wait for
            // ReadyAsync here, which would make the next switch wait on itself.
            await deviceGate.WaitAsync(lifetime).ConfigureAwait(false);
            try
            {
                bool requiresDisconnect;
                lock (_gate)
                {
                    if (!IsCurrentPolicyLocked(update, revision)) return;
                    requiresDisconnect = update.RequiresDisconnect || !device.IsEnabled;
                    // A new disable received during the await below sets this again.
                    update.RequiresDisconnect = false;
                }
                if (requiresDisconnect)
                    await StopDeviceCoreAsync(device, lifetime, manual: false).ConfigureAwait(false);
                lock (_gate)
                    if (!IsCurrentPolicyLocked(update, revision)) return;
                if (device.IsEnabled && device.AutoReconnect)
                {
                    Task cancelledRetry = Task.CompletedTask;
                    lock (_gate)
                        if (_reconnecting.TryGetValue(device.Id, out var retry) && retry.Cancellation.IsCancellationRequested)
                            cancelledRetry = retry.Task;
                    await cancelledRetry.ConfigureAwait(false);
                    lifetime.ThrowIfCancellationRequested();
                    if (device.IsEnabled && device.AutoReconnect)
                    {
                        lock (_gate)
                        {
                            if (!IsCurrentPolicyLocked(update, revision) || !device.IsEnabled || !device.AutoReconnect) return;
                            _reconnectFailures.Remove(device.Id);
                            _manualDisconnects.Remove(device.Id);
                            _connectionIntents.Add(device.Id);
                        }
                        OnConnectionClosed(this, new CommunicationConnectionClosedEventArgs(device.Id));
                    }
                }
            }
            finally { deviceGate.Release(); }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { LogWarning($"应用通讯连接策略失败：设备={device.Name}，{ex.Message}"); }
    }

    /// <summary>Manual stop cancels and drains the retry before disposing the active adapter.</summary>
    public async Task DisconnectDeviceAsync(string deviceId)
    {
        ConfiguredCommunication? device;
        CancellationToken lifetime;
        lock (_gate)
        {
            device = _activeSolution?.Communications.Find(d => d.Id == deviceId);
            lifetime = _switchCancellation?.Token ?? CancellationToken.None;
            if (_disposed || device == null) return;
            // Invalidate before waiting for a manual connect or policy worker's gate.
            _retiredDevices.Add(device);
            _policyUpdates.Remove(deviceId);
            _manualDisconnects.Add(deviceId);
            _connectionIntents.Remove(deviceId);
            CancelDeviceLifetimeLocked(deviceId);
        }
        var deviceGate = _deviceGates.GetOrAdd(deviceId, _ => new SemaphoreSlim(1, 1));
        await deviceGate.WaitAsync().ConfigureAwait(false);
        try { await StopDeviceCoreAsync(device, lifetime, manual: true).ConfigureAwait(false); }
        finally { deviceGate.Release(); }
    }

    /// <summary>Disconnect once without disabling the user's AutoReconnect policy.</summary>
    public async Task DisconnectConnectionAsync(string deviceId)
    {
        ConfiguredCommunication? device;
        CancellationToken lifetime;
        lock (_gate)
        {
            device = _activeSolution?.Communications.Find(d => d.Id == deviceId);
            lifetime = _switchCancellation?.Token ?? CancellationToken.None;
            if (_disposed || device == null) return;
            _policyUpdates.Remove(deviceId);
            _manualDisconnects.Add(deviceId);
            _connectionIntents.Remove(deviceId);
            CancelDeviceLifetimeLocked(deviceId);
        }
        var deviceGate = _deviceGates.GetOrAdd(deviceId, _ => new SemaphoreSlim(1, 1));
        await deviceGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopDeviceCoreAsync(device, lifetime, manual: true).ConfigureAwait(false);
            lock (_gate)
            {
                if (!IsCurrentDeviceLocked(device, lifetime) || !device.IsEnabled || !device.AutoReconnect) return;
                _reconnectFailures.Remove(deviceId);
                _manualDisconnects.Remove(deviceId);
                _connectionIntents.Add(deviceId);
                OnConnectionClosed(this, new CommunicationConnectionClosedEventArgs(deviceId));
            }
        }
        finally { deviceGate.Release(); }
    }

    private async Task StopDeviceCoreAsync(ConfiguredCommunication device, CancellationToken lifetime, bool manual)
    {
        var deviceId = device.Id;
        Task pending;
        lock (_gate)
        {
            if (!IsCurrentDeviceLocked(device, lifetime)) return;
            if (manual)
            {
                // A queued old enable must not undo a later explicit stop/delete.
                _policyUpdates.Remove(deviceId);
                _manualDisconnects.Add(deviceId);
                _connectionIntents.Remove(deviceId);
            }
            else if (_activeSolution?.Communications.Find(d => d.Id == deviceId)?.IsEnabled != true)
                _connectionIntents.Remove(deviceId);
            CancelDeviceLifetimeLocked(deviceId);
            if (_reconnecting.TryGetValue(deviceId, out var attempt))
            {
                attempt.Cancellation.Cancel();
                pending = attempt.Task;
            }
            else pending = Task.CompletedTask;
        }
        await pending.ConfigureAwait(false);
        Task disconnect;
        lock (_gate)
        {
            if (!IsCurrentDeviceLocked(device, lifetime)) return;
            // Runtime removes the current adapter synchronously before its first await.
            // A solution change cannot interleave between this guard and removal.
            disconnect = Composition.Runtime.DisconnectAsync(deviceId);
        }
        await disconnect.ConfigureAwait(false);
    }

    public async Task ConnectDeviceAsync(CommunicationDeviceSnapshot configuration)
    {
        ConfiguredCommunication device;
        CancellationToken lifetime;
        CancellationToken requestToken;
        Task ready;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            device = _activeSolution?.Communications.Find(d => d.Id == configuration.Id)
                ?? throw new InvalidOperationException("通讯设备已删除或不属于当前方案。");
            lifetime = _switchCancellation?.Token ?? CancellationToken.None;
            requestToken = GetDeviceLifetimeLocked(device).Token;
            ready = _switchTask;
        }
        await ready.WaitAsync(requestToken).ConfigureAwait(false);
        var deviceGate = _deviceGates.GetOrAdd(configuration.Id, _ => new SemaphoreSlim(1, 1));
        await deviceGate.WaitAsync(lifetime).ConfigureAwait(false);
        try
        {
            lock (_gate)
            {
                if (!IsCurrentDeviceLocked(device, lifetime) || _retiredDevices.Contains(device) || requestToken.IsCancellationRequested || !device.IsEnabled)
                    throw new OperationCanceledException("通讯设备已禁用、删除或方案已切换。");
                _reconnectFailures.Remove(configuration.Id);
                _manualDisconnects.Remove(configuration.Id);
                _connectionIntents.Add(configuration.Id);
            }
            await StopDeviceCoreAsync(device, lifetime, manual: false).ConfigureAwait(false);
            lifetime.ThrowIfCancellationRequested();
            CancellationToken deviceToken;
            lock (_gate)
            {
                if (!IsCurrentDeviceLocked(device, lifetime) || _retiredDevices.Contains(device) || !device.IsEnabled || _manualDisconnects.Contains(device.Id))
                    throw new OperationCanceledException("通讯设备连接已取消。");
                deviceToken = GetDeviceLifetimeLocked(device).Token;
            }
            try { await Composition.Runtime.ConnectAsync(configuration, deviceToken).ConfigureAwait(false); }
            catch
            {
                if (!deviceToken.IsCancellationRequested)
                    OnConnectionClosed(this, new CommunicationConnectionClosedEventArgs(configuration.Id));
                throw;
            }
        }
        finally { deviceGate.Release(); }
    }

    private async Task ReconnectAsync(ConfiguredCommunication device, ReconnectAttempt attempt)
    {
        var token = attempt.Cancellation.Token;
        var ownsConnection = false;
        try
        {
            var delaySeconds = 1;
            while (!token.IsCancellationRequested && device.IsEnabled && device.AutoReconnect)
            {
                lock (_gate)
                {
                    if (_reconnectFailures.GetValueOrDefault(device.Id) >= MaxReconnectFailures) return;
                    attempt.Waiting = true;
                    attempt.RetryDueTimestamp = System.Diagnostics.Stopwatch.GetTimestamp() +
                        (long)(delaySeconds * (double)System.Diagnostics.Stopwatch.Frequency);
                }
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (_disposed || !device.IsEnabled || !device.AutoReconnect || !_connectionIntents.Contains(device.Id) || _manualDisconnects.Contains(device.Id) ||
                        _activeSolution?.Communications.Find(d => d.Id == device.Id) != device) return;
                    attempt.ConnectionLostAgain = false;
                    attempt.Waiting = false;
                }
                VisionLogger.Instance.Log(LogLevel.Info, $"通讯自动重连尝试：设备={device.Name}({device.Id}), 等待={delaySeconds}秒",
                    "CommunicationApplication", target: LogTarget.File | LogTarget.UI);
                try
                {
                    if (!Composition.Runtime.IsConnected(device.Id))
                    {
                        token.ThrowIfCancellationRequested();
                        ownsConnection = true;
                        await Composition.Runtime.ConnectOwnedAsync(CommunicationDeviceSnapshotFactory.Create(device), attempt.ConnectionOwner, token)
                            .ConfigureAwait(false);
                    }
                    token.ThrowIfCancellationRequested();
                    lock (_gate)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!device.IsEnabled || !device.AutoReconnect || _disposed ||
                            _activeSolution?.Communications.Find(d => d.Id == device.Id) != device)
                            return;
                        if (device.IsModbus)
                            Composition.Polling.Start(device.Id, device.PollInterval,
                                device.ModbusAddressPoints.Where(point => point.PollEnabled && point.AccessMode != "只写")
                                    .Select(point => new ModbusPollAddress(point.Id, point.StartAddress, point.Count, point.TimeoutMs)));
                    }
                    if (device.IsOpcUa)
                        await MonitorOpcUaPointsAsync(device.Id,
                            (device.OpcUaDataPoints ?? new()).Select(point => point.NodeId), token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (!Composition.Runtime.IsConnected(device.Id))
                        throw new InvalidOperationException("通讯重连后会话再次失效，将继续重试。");
                    lock (_gate)
                    {
                        if (token.IsCancellationRequested || _disposed ||
                            !ReferenceEquals(_activeSolution?.Communications.Find(d => d.Id == device.Id), device)) return;
                        _reconnectFailures.Remove(device.Id);
                    }
                    VisionLogger.Instance.Log(LogLevel.Info, $"通讯自动重连成功：设备={device.Name}({device.Id})",
                        "CommunicationApplication", target: LogTarget.File | LogTarget.UI);
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    int failures;
                    lock (_gate)
                    {
                        if (token.IsCancellationRequested || _disposed ||
                            !ReferenceEquals(_activeSolution?.Communications.Find(d => d.Id == device.Id), device) ||
                            !_reconnecting.TryGetValue(device.Id, out var currentAttempt) ||
                            !ReferenceEquals(currentAttempt, attempt)) return;
                        _connectionErrors[device.Id] = ex.Message;
                        failures = _reconnectFailures.GetValueOrDefault(device.Id) + 1;
                        _reconnectFailures[device.Id] = failures;
                        // Update the same setting bound by every protocol's switch.
                        // Its policy notification cancels this worker; a subsequent
                        // user enable drains it before resetting the retry budget.
                        if (failures >= MaxReconnectFailures)
                            device.AutoReconnect = false;
                    }
                    if (failures >= MaxReconnectFailures)
                    {
                        LogWarning($"通讯设备“{device.Name}”自动重连已停止并关闭开关：连续失败{MaxReconnectFailures}次。最后原因：{ex.Message}。可手动连接或开启自动重连开始新一轮。 ");
                        return;
                    }
                    LogWarning($"通讯设备“{device.Name}”重连失败（{failures}/{MaxReconnectFailures}），{Math.Min(delaySeconds * 2, 30)} 秒后重试：{ex.Message}");
                    delaySeconds = Math.Min(delaySeconds * 2, 30);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            // Retire only the adapter created by this attempt. A duplicate connection
            // attempt or a later manual connection must never be closed by old cleanup.
            try
            {
                if (ownsConnection && (token.IsCancellationRequested || !device.IsEnabled || !device.AutoReconnect))
                    await Composition.Runtime.DisconnectOwnedAsync(device.Id, attempt.ConnectionOwner).ConfigureAwait(false);
            }
            finally
            {
                lock (_gate)
                {
                    if (_reconnecting.TryGetValue(device.Id, out var current) && ReferenceEquals(current, attempt))
                        _reconnecting.Remove(device.Id);
                    var retry = attempt.ConnectionLostAgain && !token.IsCancellationRequested &&
                        _reconnectFailures.GetValueOrDefault(device.Id) < MaxReconnectFailures;
                    attempt.Cancellation.Dispose();
                    // Do not lose a close event that arrived between the final connected check
                    // and removal of this retry worker.
                    if (retry) OnConnectionClosed(this, new CommunicationConnectionClosedEventArgs(device.Id));
                }
            }
        }
    }

    private static void LogWarning(string message) =>
        VisionLogger.Instance.Log(LogLevel.Warning, message, "CommunicationApplication");

    public async Task MonitorOpcUaPointAsync(string communicationId, string nodeId, CancellationToken token = default)
    {
        CancellationToken lifetime;
        lock (_gate)
        {
            var device = _activeSolution?.Communications.Find(d => d.Id == communicationId);
            if (_disposed || !_connectionsReady || device?.IsEnabled != true || _manualDisconnects.Contains(communicationId)) return;
            lifetime = GetDeviceLifetimeLocked(device).Token;
        }
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
            var device = _activeSolution?.Communications.Find(d => d.Id == communicationId);
            if (device?.IsEnabled != true || _manualDisconnects.Contains(communicationId)) return;
            lifetime = GetDeviceLifetimeLocked(device).Token;
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
            token.ThrowIfCancellationRequested();
            if (mode == "周期读取")
            {
                await _opcUaPolling.StartAsync(communicationId, nodeId, interval, lifetime).ConfigureAwait(false);
                _monitorStatuses[point.Id] = $"周期读取间隔 {interval}ms；实际周期受读取耗时影响。";
            }
            else
            {
                await Composition.Runtime.SubscribeOpcUaValueAsync(communicationId, nodeId, interval, token).ConfigureAwait(false);
                var revised = await Composition.Runtime.GetOpcUaMonitoringIntervalsAsync(communicationId, nodeId, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                _monitorStatuses[point.Id] = revised is { } actual
                    ? $"服务器接受：采样 {actual.Sampling}ms，发布 {actual.Publishing}ms。"
                    : "尚无服务器实际订阅间隔。";
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            token.ThrowIfCancellationRequested();
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
            _connectionsReady = false;
            foreach (var device in _observedDevices) device.PropertyChanged -= ConnectionPolicyChanged;
            _observedDevices.Clear();
            Heartbeats.Runner.Update(Array.Empty<CommunicationHeartbeatJob>());
            Automation.Reset(null);
            Receives.Configure(Array.Empty<SunEyeVision.Plugin.SDK.Models.Communication.CommunicationReceiveSource>(), reset: true);
            Composition.Runtime.DataReceived -= OnAutomationData;
            Composition.Runtime.OpcUaPointValueChanged -= OnAutomationOpcUa;
            Composition.Polling.BatchReceived -= OnAutomationModbus;
            _solutions.SolutionOpened -= OnSolutionOpened;
            _solutions.SolutionClosed -= OnSolutionClosed;
            _solutions.SolutionSaving -= OnSolutionSaving;
            Composition.Runtime.ConnectionClosed -= OnConnectionClosed;
            Composition.Runtime.StatusChanged -= OnReceiveStatus;
            _switchCancellation?.Cancel();
            foreach (var id in _deviceLifetimes.Keys.ToArray()) CancelDeviceLifetimeLocked(id);
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
