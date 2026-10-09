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
    string Action, object? TriggerValue);

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
    public Func<CommunicationTriggerRequest, CancellationToken, Task<bool>>? Dispatcher { get; set; }
    public Func<string, bool?>? CameraConnectionObserver { get; set; }
    public event Action<LogLevel, string>? Diagnostic;

    public CommunicationAutomationService(CommunicationRuntimeManager runtime) => _runtime = runtime;
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(
        JsonSerializer.Serialize(value, WorkflowSerializationOptions.Default), WorkflowSerializationOptions.Default)!;

    public void Reset(Solution? solution)
    {
        lock (_gate)
        {
            _lifetime.Cancel(); _retired.Add(_lifetime); _lifetime = new();
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
            _rules = (solution?.CommunicationTriggers ?? new()).Select(Copy).ToArray();
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
            catch (DecoderFallbackException) { return; }
            foreach (var rule in _rules.Where(r => r.Source == "String"))
                if (rule.MatchMode == "不匹配" || (!string.IsNullOrEmpty(rule.Text) &&
                    (rule.MatchMode == "部分匹配" ? text.Contains(rule.Text, StringComparison.Ordinal) : text == rule.Text)))
                    Dispatch(rule, text);
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
        }
    }

    private void Accept(CommunicationReceiveEventViewModel state, CommunicationReceiveOutcome outcome)
    {
        if (!outcome.IsSuccess) return;
        foreach (var rule in _rules.Where(r => r.Source == "Event" && r.EventId == state.EventId))
        {
            object? value = null;
            if (rule.FieldId.HasValue)
            {
                var field = state.AssemblyOutputs.FirstOrDefault(f => f.FieldId == rule.FieldId);
                if (field == null) { Warn($"触发 {rule.Id} 的输出字段已失效，已跳过。"); continue; }
                value = field.Result;
            }
            Dispatch(rule, value);
        }
    }

    private void Dispatch(CommunicationTriggerDefinition rule, object? value)
    {
        var dispatcher = Dispatcher;
        if (dispatcher == null) { Warn($"触发 {rule.Id} 尚无执行宿主，已跳过。"); return; }
        var token = _lifetime.Token;
        var request = new CommunicationTriggerRequest(rule.Id, rule.Command, rule.TargetId, rule.Action, value);
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
