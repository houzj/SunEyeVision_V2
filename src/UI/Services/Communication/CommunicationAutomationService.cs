using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.Workflow;
using ConfiguredCommunication = SunEyeVision.Workflow.Communication;

namespace SunEyeVision.UI.Services.Communication;

public sealed record CommunicationTriggerRequest(Guid RuleId, string Command, string TargetId,
    string Action, object? TriggerValue)
{
    public IReadOnlyList<CommunicationParameterAssignment> Assignments { get; init; } = Array.Empty<CommunicationParameterAssignment>();
    public CommunicationUploadSnapshot? Upload { get; init; }
}

/// <summary>Owns real-sample parsing, trigger dispatch and status responses, independently of the manager window.</summary>
public sealed class CommunicationAutomationService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly CommunicationRuntimeManager _runtime;
    private CancellationTokenSource _lifetime = new();
    private readonly List<CancellationTokenSource> _retired = new();
    private readonly HashSet<Task> _tasks = new();
    private Task _cameraTask = Task.CompletedTask;
    private Dictionary<string, ConfiguredCommunication> _devices = new();
    private Dictionary<Guid, (string Signature, CommunicationReceiveEventViewModel State)> _receivers = new();
    private CommunicationTriggerDefinition[] _rules = Array.Empty<CommunicationTriggerDefinition>();
    private CommunicationResponseConfiguration _responses = new();
    private Dictionary<string, string> _workflowIds = new(), _cameraIds = new();
    private readonly Dictionary<string, bool> _workflowStates = new(), _cameraStates = new();
    private readonly Dictionary<string, Task> _responseTails = new();
    private bool _cameraWatchStarted;
    private long _version;
    private bool _disposed;
    private Guid _uploadSession = Guid.NewGuid();
    internal Guid UploadSession { get { lock (_gate) return _uploadSession; } }
    public Func<CommunicationTriggerRequest, CancellationToken, Task<bool>>? Dispatcher { get; set; }
    public Func<string, bool?>? CameraConnectionObserver { get; set; }
    public event Action<LogLevel, string>? Diagnostic;
    public event Action<CommunicationUploadSnapshot>? DataUploaded;

    public CommunicationAutomationService(CommunicationRuntimeManager runtime) => _runtime = runtime;
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(
        JsonSerializer.Serialize(value, WorkflowSerializationOptions.Default), WorkflowSerializationOptions.Default)!;

    public void Reset(Solution? solution)
    {
        lock (_gate)
        {
            _lifetime.Cancel(); _retired.Add(_lifetime); _lifetime = new();
            _uploadSession = Guid.NewGuid();
            _receivers.Clear(); _workflowStates.Clear(); _cameraStates.Clear();
            _responseTails.Clear(); _cameraWatchStarted = false;
            Refresh(solution);
        }
    }

    public void Refresh(Solution? solution)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _version++;
            _devices = (solution?.Communications ?? new()).Select(Copy).ToDictionary(d => d.Id);
            var definitions = solution?.CommunicationTriggers ?? new();
            _rules = definitions.Where(r => r.Source is "Event" or "GlobalData").Select(Copy).ToArray();
            if (solution != null)
            {
                EnsureIdentifiers(solution.Workflows.Where(w => !w.IsSubroutine).Select(w => w.Id),
                    solution.CommunicationResponses.WorkflowIdentifiers);
                EnsureIdentifiers(solution.Devices.Where(d => d.Type == DeviceType.Camera).Select(d => d.Id),
                    solution.CommunicationResponses.CameraIdentifiers);
            }
            _responses = Copy(solution?.CommunicationResponses ?? new());
            var next = new Dictionary<Guid, (string, CommunicationReceiveEventViewModel)>();
            foreach (var definition in solution?.CommunicationEvents ?? new())
            {
                var signature = JsonSerializer.Serialize(definition, WorkflowSerializationOptions.Default);
                next[definition.EventId] = _receivers.TryGetValue(definition.EventId, out var old) && old.Signature == signature
                    ? old : (signature, CommunicationReceiveStateFactory.Create(definition));
            }
            _receivers = next;
            _workflowIds = (solution?.Workflows ?? new()).Where(w => !w.IsSubroutine)
                .Select(w => (w.Id, Number: _responses.WorkflowIdentifiers[w.Id].ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .ToDictionary(x => x.Id, x => x.Number);
            _cameraIds = (solution?.Devices ?? new()).Where(d => d.Type == DeviceType.Camera)
                .Select(d => (d.Id, Number: _responses.CameraIdentifiers[d.Id].ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .ToDictionary(x => x.Id, x => x.Number);
        }
    }

    public void ProcessRaw(string deviceId, byte[] data, DateTimeOffset timestamp)
    {
        lock (_gate)
        {
            if (_disposed || !_devices.TryGetValue(deviceId, out var device) || !device.IsRawProtocol) return;
            foreach (var state in _receivers.Values.Select(v => v.State).Where(s => s.SelectedDeviceId == deviceId))
                Accept(state, CommunicationReceiveProcessor.Process(state, data, timestamp));
            if (!device.EnableDataUpload) return;
            string text;
            try { text = new UTF8Encoding(false, true).GetString(data); }
            catch (DecoderFallbackException)
            {
                ProcessUploadedData(new(_uploadSession, deviceId, null, timestamp, "Byte[]", data, rawData: data));
                return;
            }
            ProcessUploadedData(new(_uploadSession, deviceId, null, timestamp, "String", text, rawData: data));
        }
    }

    private static void EnsureIdentifiers(IEnumerable<string> ids, Dictionary<string, int> numbers)
    {
        var next = numbers.Count == 0 ? 0 : numbers.Values.Max();
        foreach (var id in ids)
            if (!numbers.ContainsKey(id)) numbers[id] = checked(++next);
    }

    public void ProcessModbus(ModbusPollResult sample)
    {
        lock (_gate)
        {
            if (_disposed || !_devices.TryGetValue(sample.CommunicationId, out var device) || !device.IsModbus) return;
            var point = device.ModbusAddressPoints.FirstOrDefault(p => p.Id == sample.AddressId);
            if (point == null || point.StartAddress != sample.StartAddress || point.Count != sample.Count) return;
            foreach (var state in _receivers.Values.Select(v => v.State).Where(s =>
                s.SelectedDeviceId == sample.CommunicationId && s.SelectedAddress == sample.AddressId))
                Accept(state, CommunicationReceiveProcessor.ProcessModbus(state, sample.Values, sample.Timestamp));
            if (device.EnableDataUpload && sample.Values.Length == sample.Count)
            {
                object value = sample.Values;
                var type = "UInt16[]";
                if (point.IntBits == "16bit" && sample.Values.Length == 1)
                {
                    var word = sample.Values[0];
                    if (point.ByteOrder is "BADC" or "DCBA") word = (ushort)((word >> 8) | (word << 8));
                    else if (point.ByteOrder is not ("ABCD" or "CDAB")) { Warn("Modbus 上传字序无效，已跳过。"); return; }
                    value = word; type = "UInt16";
                }
                else if (point.IntBits == "32bit" && sample.Values.Length == 2)
                {
                    uint bits = ((uint)sample.Values[0] << 16) | sample.Values[1];
                    bits = point.ByteOrder switch { "ABCD" => bits, "CDAB" => (bits << 16) | (bits >> 16),
                        "BADC" => ((bits & 0x00ff00ff) << 8) | ((bits & 0xff00ff00) >> 8),
                        "DCBA" => System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(bits), _ => bits };
                    if (point.ByteOrder is not ("ABCD" or "BADC" or "CDAB" or "DCBA"))
                    { Warn("Modbus 上传字序无效，已跳过。"); return; }
                    value = unchecked((int)bits); type = "Int32";
                }
                ProcessUploadedData(new(_uploadSession, device.Id, point.Id, sample.Timestamp, type, value));
            }
        }
    }

    public void ProcessOpcUa(OpcUaPointValueEventArgs sample)
    {
        lock (_gate)
        {
            if (_disposed || !_devices.TryGetValue(sample.CommunicationId, out var device)) return;
            var point = device.OpcUaDataPoints?.FirstOrDefault(p => p.NodeId == sample.NodeId);
            if (point == null) return;
            foreach (var state in _receivers.Values.Select(v => v.State).Where(s =>
                s.SelectedDeviceId == sample.CommunicationId && s.SelectedAddress == point.Id))
                Accept(state, CommunicationReceiveProcessor.ProcessOpcUa(state, sample));
            if (device.EnableDataUpload && sample.Value is string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double)
                ProcessUploadedData(new(_uploadSession, device.Id, point.Id, sample.Timestamp,
                    sample.BuiltInType.ToString(), sample.Value, Opc.Ua.StatusCode.IsGood(sample.StatusCode) &&
                    sample.IsSupportedScalar && string.IsNullOrEmpty(sample.ErrorMessage)));
        }
    }

    internal void ProcessUploadedData(CommunicationUploadSnapshot sample)
    {
        lock (_gate)
        {
            if (_disposed || sample.SessionId != _uploadSession || !_devices.TryGetValue(sample.DeviceId, out var device) ||
                !device.EnableDataUpload) return;
            foreach (var subscriber in DataUploaded?.GetInvocationList() ?? Array.Empty<Delegate>())
            {
                try { ((Action<CommunicationUploadSnapshot>)subscriber)(sample); }
                catch (Exception ex) { Warn($"上传订阅处理失败：{ex.Message}"); }
            }
            // A subscriber may initiate a solution transition. Never dispatch a now-retired upload.
            if (sample.SessionId != _uploadSession || _disposed) return;
            foreach (var rule in _rules.Where(r => r.Source == "GlobalData"))
            {
                var result = CommunicationTriggerEvaluation.EvaluateUpload(rule, sample);
                if (result.Error != null) { Warn($"全局触发 {rule.Id} 已跳过：{result.Error}。"); continue; }
                if (result.Matches) Dispatch(rule, result.TriggerValue, upload: sample);
            }
        }
    }

    public void ResetDeviceHistory(string deviceId, string? nodeId = null)
    {
        lock (_gate)
            foreach (var state in _receivers.Values.Select(v => v.State).Where(s => s.SelectedDeviceId == deviceId &&
                (nodeId == null || _devices.GetValueOrDefault(deviceId)?.OpcUaDataPoints.Any(p => p.NodeId == nodeId && p.Id == s.SelectedAddress) == true)))
                CommunicationReceiveProcessor.ResetHistory(state);
    }

    private void Accept(CommunicationReceiveEventViewModel state, CommunicationReceiveOutcome outcome)
    {
        if (!outcome.IsSuccess) return;
        foreach (var rule in _rules.Where(r => r.Source == "Event" && r.EventId == state.EventId))
        {
            try
            {
                var fields = new Dictionary<Guid, CommunicationFieldSample>();
                if (!state.IsProtocolCompare)
                    foreach (var field in state.Outputs.Where(f => f.Value != null))
                        fields.Add(field.FieldId, new(field.FieldId, field.Name, field.DataType, field.Value!));
                else
                    foreach (var field in state.AssemblyOutputs)
                    {
                        var type = CommunicationTriggerEvaluation.ScalarType(field.DataType);
                        if (type != null && CommunicationTriggerEvaluation.TryParse(field.Result, type, out var value))
                            fields.Add(field.FieldId, new(field.FieldId, field.Name, field.DataType, value!));
                    }
                var outputIds = state.IsProtocolCompare ? state.AssemblyOutputs.Select(f => f.FieldId).ToArray()
                    : state.Outputs.Select(f => f.FieldId).ToArray();
                var condition = CommunicationTriggerEvaluation.EvaluateEquality(rule, state.EventId, outputIds, fields);
                if (condition.Error != null)
                { Warn($"触发 {rule.Id} 的条件无效，已跳过：{condition.Error}。"); continue; }
                if (!condition.Matches)
                { Emit(LogLevel.Info, $"触发 {rule.Id}：动作条件不相等，未执行。"); continue; }
                // Conditions do not assign workflow node parameters.
                Dispatch(rule, condition.TriggerValue);
            }
            catch (Exception ex) { Warn($"触发 {rule.Id} 的绑定无效，已跳过：{ex.Message}"); }
        }
    }

    private void Dispatch(CommunicationTriggerDefinition rule, object? value,
        IReadOnlyList<CommunicationParameterAssignment>? assignments = null, CommunicationUploadSnapshot? upload = null)
    {
        var dispatcher = Dispatcher;
        if (dispatcher == null) { Warn($"触发 {rule.Id} 尚无执行宿主，已跳过。"); return; }
        var token = _lifetime.Token;
        var request = new CommunicationTriggerRequest(rule.Id, rule.Command, rule.TargetId, rule.Action, value)
        { Assignments = assignments ?? Array.Empty<CommunicationParameterAssignment>(), Upload = upload };
        Task<bool> pending;
        try
        {
            // Acquire/reject the shared host lease at sample arrival, before a thread-pool handoff.
            token.ThrowIfCancellationRequested();
            pending = dispatcher(request, token);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { Emit(LogLevel.Error, $"通讯触发失败：{ex.Message}"); return; }
        Track(async () =>
        {
            if (!await pending.ConfigureAwait(false))
                Warn($"触发 {request.RuleId}：目标 {request.TargetId} 忙碌，已跳过，不排队。");
        });
    }

    public void SolutionLoaded()
    {
        lock (_gate)
        {
            if (_disposed) return;
            QueueResponse(_responses.SolutionLoaded, _responses.SolutionLoaded.Content, null, _responses.SolutionLoaded.DelaySeconds);
            if (_cameraWatchStarted) return;
            _cameraWatchStarted = true;
            var token = _lifetime.Token;
            _cameraTask = Track(async () =>
            {
                while (true)
                {
                    await Task.Delay(500, token).ConfigureAwait(false);
                    lock (_gate)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!_responses.CameraState.Enabled || CameraConnectionObserver == null) continue;
                        foreach (var id in _cameraIds.Keys.ToArray())
                        {
                            var state = CameraConnectionObserver(id);
                            if (!state.HasValue) continue;
                            if (!_cameraStates.ContainsKey(id)) _cameraStates[id] = state.Value;
                            else CameraStateChanged(id, state.Value);
                        }
                    }
                }
            });
        }
    }

    public void WorkflowStateChanged(string workflowId, bool busy)
    {
        lock (_gate)
        {
            if (_disposed || !_workflowIds.TryGetValue(workflowId, out var number)) return;
            if (_workflowStates.TryGetValue(workflowId, out var previous) && previous == busy) return;
            _workflowStates[workflowId] = busy;
            QueueResponse(_responses.WorkflowState, busy ? _responses.WorkflowState.AlternateContent : _responses.WorkflowState.Content, number);
        }
    }

    public void CameraStateChanged(string deviceId, bool connected)
    {
        lock (_gate)
        {
            if (_disposed || !_cameraIds.TryGetValue(deviceId, out var number)) return;
            if (_cameraStates.TryGetValue(deviceId, out var previous) && previous == connected) return;
            _cameraStates[deviceId] = connected;
            QueueResponse(_responses.CameraState, connected ? _responses.CameraState.Content : _responses.CameraState.AlternateContent, number);
        }
    }

    private void QueueResponse(CommunicationResponseDefinition configured, string content, string? id, double delay = 0)
    {
        if (_disposed || !configured.Enabled) return;
        if (!_devices.TryGetValue(configured.DeviceId, out var device)) { Warn("响应绑定设备已失效，未发送。"); return; }
        var config = Copy(configured);
        var token = _lifetime.Token;
        var version = _version;
        var previous = _responseTails.GetValueOrDefault(device.Id, Task.CompletedTask);
        _responseTails[device.Id] = Track(async () =>
        {
            await previous.ConfigureAwait(false);
            if (!double.IsFinite(delay) || delay < 0 || delay > 86400) throw new FormatException("响应延迟应在 0～86400 秒之间。");
            if (delay > 0) await Task.Delay(TimeSpan.FromSeconds(delay), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            lock (_gate) if (version != _version) return;
            if (!device.IsEnabled || !_runtime.IsConnected(device.Id)) throw new InvalidOperationException("响应设备未连接，未发送。");
            var sender = new CommunicationSendEventService(_runtime);
            var plan = sender.Prepare(CommunicationResponseFormatter.Create(config, device, content, id), device);
            token.ThrowIfCancellationRequested();
            if (await sender.SendAsync(plan, token).ConfigureAwait(false) == 0) throw new InvalidOperationException("响应无接收端。");
        });
    }

    private Task Track(Func<Task> action)
    {
        var task = Task.Run(async () =>
        {
            try { await action().ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Emit(LogLevel.Error, $"通讯自动执行失败：{ex.Message}"); }
        });
        _tasks.Add(task);
        _ = task.ContinueWith(t => { lock (_gate) _tasks.Remove(t); }, TaskScheduler.Default);
        return task;
    }

    private void Warn(string message) => Emit(LogLevel.Warning, message);
    private void Emit(LogLevel level, string message)
    {
        VisionLogger.Instance.Log(level, message, "CommunicationAutomation");
        foreach (var handler in Diagnostic?.GetInvocationList() ?? Array.Empty<Delegate>())
            try { ((Action<LogLevel, string>)handler)(level, message); } catch { }
    }

    public Task DrainAsync() { lock (_gate) return Task.WhenAll(_tasks.Where(task => task != _cameraTask).ToArray()); }
    public async ValueTask DisposeAsync()
    {
        lock (_gate) { _disposed = true; _lifetime.Cancel(); Dispatcher = null; }
        await DrainAsync().ConfigureAwait(false);
        await _cameraTask.ConfigureAwait(false);
        _lifetime.Dispose(); foreach (var source in _retired) source.Dispose();
    }
}
