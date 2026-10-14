using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.Plugin.SDK.Models.Communication;

public sealed record CommunicationReceiveSource(string DeviceId, string TargetId, string DisplayName, string DataType,
    string ConfigurationKey = "", string DeviceName = "", string TargetName = "", bool ActiveRead = false);
public sealed record CommunicationReceiveSample(Guid Id, string DeviceId, string TargetId, DateTimeOffset Timestamp,
    object Value, bool IsGood, long Generation, DateTimeOffset? SourceTimestamp = null, long Sequence = 0);
public sealed record ReceiveVariableSource(string Id, string Name, string Group, string DataType, bool HasProducer = false,
    string ProducerNodeId = "", string ProducerField = "", string ProducerWorkflowId = "");
public interface IReceiveVariableInputProvider { IReadOnlyList<string> GetReceiveVariableIds(); }
public sealed record ReceiveVariableValue(ReceiveVariableSource Source, object Value);
public interface ICommunicationReceiveService
{
    IReadOnlyList<CommunicationReceiveSource> GetSources();
    Task<CommunicationReceiveSample> ReadAsync(string deviceId, string targetId, CancellationToken token = default);
    Task ConsumeAsync(string deviceId, string targetId, string consumerId, Action<CommunicationReceiveSample> commit,
        CancellationToken token = default) => throw new NotSupportedException("宿主尚未支持逐次报文接收。");
    IReadOnlyList<ReceiveVariableSource> GetVariables();
    IReadOnlyDictionary<string, ReceiveVariableValue> ReadVariables(IReadOnlyList<string> ids);
}

/// <summary>Current-message storage and execution-time reads; no run-scoped receive snapshot.</summary>
public sealed partial class CommunicationReceiveService : ICommunicationReceiveService
{
    private readonly object _gate = new();
    private Dictionary<(string, string), CommunicationReceiveSource> _sources = new();
    private readonly Dictionary<(string, string), CommunicationReceiveSample> _latest = new();
    private readonly Dictionary<string, long> _generations = new();
    private readonly HashSet<string> _disconnected = new();
    private readonly Dictionary<string, string> _emptyReasons = new();
    private readonly Dictionary<(string, string), TaskCompletionSource<bool>> _changed = new();
    private long _nextGeneration;
    public Func<CommunicationReceiveSource, CancellationToken, Task<(object Value, DateTimeOffset? SourceTime)>>? ActiveReader { get; set; }
    public Func<IReadOnlyList<ReceiveVariableSource>>? VariableCatalog { get; set; }
    public Func<IReadOnlyList<string>, IReadOnlyDictionary<string, ReceiveVariableValue>>? VariableReader { get; set; }
    public IReadOnlyList<ReceiveVariableSource> GetVariables() => VariableCatalog?.Invoke() ?? Array.Empty<ReceiveVariableSource>();
    public IReadOnlyDictionary<string, ReceiveVariableValue> ReadVariables(IReadOnlyList<string> ids) =>
        VariableReader?.Invoke(ids) ?? throw new InvalidOperationException("宿主未提供当前变量读取服务。");
    public void Configure(IEnumerable<CommunicationReceiveSource> sources, bool reset = false)
    {
        lock (_gate)
        {
            var next = sources.ToDictionary(s => (s.DeviceId, s.TargetId));
            if (reset) { _buffers.Clear(); _consumers.Clear(); _latest.Clear(); _generations.Clear(); _disconnected.Clear(); _emptyReasons.Clear(); foreach (var key in _changed.Keys.ToArray()) Signal(key); }
            foreach (var device in _sources.Keys.Where(k => !next.ContainsKey(k) || !SameDataConfiguration(_sources[k], next[k])).Select(k => k.Item1).Distinct().ToArray())
            {
                _generations[device] = ++_nextGeneration;
                ResetConsumers(device);
                _emptyReasons[device] = "设备或接收目标配置已变化，需要当前连接重新接收数据。";
                foreach (var key in _latest.Keys.Where(k => k.Item1 == device).ToArray()) _latest.Remove(key);
                SignalDevice(device);
            }
            foreach (var device in next.Keys.Select(k => k.Item1).Distinct())
                if (!_generations.ContainsKey(device)) _generations[device] = ++_nextGeneration;
            _sources = next;
        }
    }
    private static bool SameDataConfiguration(CommunicationReceiveSource a, CommunicationReceiveSource b) =>
        a.DeviceId == b.DeviceId && a.TargetId == b.TargetId && a.DataType == b.DataType &&
        a.ConfigurationKey == b.ConfigurationKey && a.ActiveRead == b.ActiveRead;
    // Signals carry no payload: after waking each reader rechecks the current session and latest value.
    private Task Changed((string, string) key)
    {
        if (!_changed.TryGetValue(key, out var signal))
            _changed[key] = signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return signal.Task;
    }
    private void Signal((string, string) key)
    { if (_changed.Remove(key, out var signal)) signal.TrySetResult(true); }
    private void SignalDevice(string deviceId)
    { foreach (var key in _changed.Keys.Where(k => k.Item1 == deviceId).ToArray()) Signal(key); }
    public IReadOnlyList<CommunicationReceiveSource> GetSources()
    { lock (_gate) return _sources.Values.ToArray(); }
    public void InvalidateDevice(string deviceId)
    {
        lock (_gate)
        {
            _generations[deviceId] = ++_nextGeneration; _disconnected.Add(deviceId);
            ResetConsumers(deviceId);
            _emptyReasons[deviceId] = "连接已重建，需要此连接重新接收数据。";
            foreach (var key in _latest.Keys.Where(k => k.Item1 == deviceId).ToArray()) _latest.Remove(key);
            SignalDevice(deviceId);
        }
    }
    public void MarkConnected(string deviceId)
    { lock (_gate) _disconnected.Remove(deviceId); }
    public CommunicationReceiveSample? Publish(string deviceId, string targetId, DateTimeOffset timestamp, object value, bool isGood = true, DateTimeOffset? sourceTimestamp = null)
    {
        lock (_gate)
        {
            if (!_sources.ContainsKey((deviceId, targetId)) || _disconnected.Contains(deviceId)) return null;
            var sample = new CommunicationReceiveSample(Guid.NewGuid(), deviceId, targetId, timestamp,
                Normalize(value), isGood, _generations[deviceId], sourceTimestamp);
            if (!_sources[(deviceId, targetId)].ActiveRead) sample = BufferSample(sample);
            if (sample.Sequence > 0 && _buffers[(deviceId, targetId)].Items.Count == 0) _latest.Remove((deviceId, targetId));
            else _latest[(deviceId, targetId)] = sample;
            Signal((deviceId, targetId)); return sample;
        }
    }
    public async Task<CommunicationReceiveSample> ReadAsync(string deviceId, string targetId, CancellationToken token = default)
    {
        CommunicationReceiveSource source; long generation; Task? changed = null;
        token.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_sources.TryGetValue((deviceId, targetId), out source!)) throw new InvalidOperationException("所选设备／目标不存在或未启用。");
            if (_disconnected.Contains(deviceId)) throw new InvalidOperationException("接收设备已断线。");
            generation = _generations[deviceId];
            if (!source.ActiveRead)
            {
                if (_latest.TryGetValue((deviceId, targetId), out var current))
                {
                    if (!current.IsGood) throw new InvalidOperationException("接收数据质量无效。");
                    return current;
                }
                changed = Changed((deviceId, targetId));
            }
        }
        if (!source.ActiveRead)
            while (true)
            {
                await changed!.WaitAsync(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (!_sources.TryGetValue((deviceId, targetId), out var now)) throw new InvalidOperationException("等待期间接收来源已删除或禁用。");
                    if (_disconnected.Contains(deviceId)) throw new InvalidOperationException("接收设备已断线。");
                    if (!SameDataConfiguration(now, source) || _generations[deviceId] != generation)
                        throw new InvalidOperationException(_emptyReasons.TryGetValue(deviceId, out var reason) ? reason : "等待期间设备会话或目标配置已变化。");
                    if (_latest.TryGetValue((deviceId, targetId), out var current))
                    {
                        if (!current.IsGood) throw new InvalidOperationException("接收数据质量无效。");
                        return current;
                    }
                    changed = Changed((deviceId, targetId));
                }
            }
        var read = await (ActiveReader ?? throw new InvalidOperationException("主动读取服务未接通。"))(source, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_sources.TryGetValue((deviceId, targetId), out var now) || !SameDataConfiguration(now, source) || _disconnected.Contains(deviceId)
                || _generations[deviceId] != generation) throw new InvalidOperationException("读取期间设备会话或目标配置已变化。");
            return new(Guid.NewGuid(), deviceId, targetId, DateTimeOffset.UtcNow, Normalize(read.Value), true, generation, read.SourceTime);
        }
    }
    public static object Normalize(object value)
    {
        if (value is Array a && value is not byte[]) { if (a.Length != 1) throw new InvalidOperationException("只支持单项值。"); value = a.GetValue(0) ?? throw new InvalidOperationException("值为空。"); }
        return value switch
        {
            BinaryPayload or string or bool => value,
            byte[] b => new BinaryPayload(b),
            byte or sbyte or short or ushort or int or uint or long => Convert.ToInt64(value),
            ulong n when n <= long.MaxValue => (long)n,
            float n when float.IsFinite(n) => (double)n,
            double n when double.IsFinite(n) => n,
            _ => throw new InvalidOperationException("不支持该来源类型；不隐式字符串化。")
        };
    }
    public static string ValueType(object value) => Normalize(value) switch
    { BinaryPayload => "Binary", string => "String", long => "Integer", double => "Double", bool => "Boolean", _ => throw new InvalidOperationException("类型不支持。") };
}
