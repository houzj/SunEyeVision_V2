using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.Plugin.SDK.Models.Communication;

public sealed partial class CommunicationReceiveService
{
    public const int MaximumBufferedMessages = 256;
    public const int MaximumBufferedBytes = 4 * 1024 * 1024;
    public const int MaximumConsumers = 4096;
    private sealed class MessageBuffer
    {
        public long Sequence, DroppedThrough, Bytes;
        public readonly Queue<(CommunicationReceiveSample Sample, int Size)> Items = new();
    }
    private sealed class Consumer { public long Sequence; public bool Busy; }
    private readonly Dictionary<(string, string), MessageBuffer> _buffers = new();
    private readonly Dictionary<(string Device, string Target, string Consumer), Consumer> _consumers = new();
    private void ResetConsumers(string device)
    {
        foreach (var key in _buffers.Keys.Where(k => k.Item1 == device).ToArray()) _buffers.Remove(key);
        foreach (var key in _consumers.Keys.Where(k => k.Device == device).ToArray()) _consumers.Remove(key);
    }
    private CommunicationReceiveSample BufferSample(CommunicationReceiveSample sample)
    {
        var key = (sample.DeviceId, sample.TargetId);
        if (!_buffers.TryGetValue(key, out var buffer)) _buffers[key] = buffer = new();
        sample = sample with { Sequence = ++buffer.Sequence };
        var size = sample.Value is BinaryPayload bytes ? bytes.Length :
            sample.Value is string text ? checked(text.Length * 2) : 16;
        buffer.Items.Enqueue((sample, size)); buffer.Bytes += size;
        while (buffer.Items.Count > MaximumBufferedMessages || buffer.Bytes > MaximumBufferedBytes)
        {
            var removed = buffer.Items.Dequeue(); buffer.Bytes -= removed.Size;
            buffer.DroppedThrough = removed.Sample.Sequence;
        }
        return sample;
    }
    /// <summary>Commit only publishes an in-memory result. Throwing or cancellation before it never advances the cursor.</summary>
    public async Task ConsumeAsync(string deviceId, string targetId, string consumerId,
        Action<CommunicationReceiveSample> commit, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerId); ArgumentNullException.ThrowIfNull(commit);
        var key = (deviceId, targetId); var consumerKey = (deviceId, targetId, consumerId);
        CommunicationReceiveSource source; long generation; Consumer consumer;
        lock (_gate)
        {
            token.ThrowIfCancellationRequested();
            if (!_sources.TryGetValue(key, out source!)) throw new InvalidOperationException("接收来源不存在或未启用。");
            if (source.ActiveRead) throw new InvalidOperationException("主动读取来源不能使用报文消费接口。");
            if (_disconnected.Contains(deviceId)) throw new InvalidOperationException("接收设备已断线。");
            generation = _generations[deviceId];
            if (!_consumers.TryGetValue(consumerKey, out consumer!))
            {
                if (_consumers.Count >= MaximumConsumers) throw new InvalidOperationException("接收消费者数量达到上限，请重载方案释放运行状态。");
                _consumers[consumerKey] = consumer = new();
            }
            if (consumer.Busy) throw new InvalidOperationException("同一接收节点已有报文领取正在执行。");
            consumer.Busy = true;
        }
        try
        {
            while (true)
            {
                Task changed;
                lock (_gate)
                {
                    token.ThrowIfCancellationRequested();
                    if (!_sources.TryGetValue(key, out var now) || !SameDataConfiguration(now, source) ||
                        !_generations.TryGetValue(deviceId, out var currentGeneration) || currentGeneration != generation ||
                        _disconnected.Contains(deviceId))
                        throw new InvalidOperationException("等待期间设备会话或来源配置已变化，请重新执行。");
                    if (_buffers.TryGetValue(key, out var buffer))
                    {
                        if (consumer.Sequence < buffer.DroppedThrough)
                            throw new InvalidOperationException($"接收缓冲溢出：报文序号 {consumer.Sequence + 1} 至 {buffer.DroppedThrough} 已丢失，请重载方案重新建立接收会话。");
                        var next = buffer.Items.FirstOrDefault(item => item.Sample.Sequence > consumer.Sequence).Sample;
                        if (next != null)
                        {
                            if (!next.IsGood) throw new InvalidOperationException($"报文 {next.Sequence} 质量无效，未消费。");
                            token.ThrowIfCancellationRequested(); commit(next); consumer.Sequence = next.Sequence; return;
                        }
                    }
                    changed = Changed(key);
                }
                await changed.WaitAsync(token).ConfigureAwait(false);
            }
        }
        finally { lock (_gate) consumer.Busy = false; }
    }
}
