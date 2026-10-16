using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using SunEyeVision.Plugin.SDK.Devices.Communication;

namespace SunEyeVision.UI.Services.Communication;

/// <summary>Process-local connection intent and actual transport state; not device values.</summary>
public sealed record CommunicationConnectionPresentation(bool IsConnectionEnabled,
    CommunicationState ActualState, bool IsWaitingForReconnect, int RetrySeconds,
    string StatusText, string FailureReason);

public sealed partial class CommunicationApplicationService
{
    private readonly ConcurrentDictionary<string, string> _connectionErrors = new(StringComparer.Ordinal);

    public CommunicationConnectionPresentation GetConnectionPresentation(string id)
    {
        // Runtime may emit a close notification; do not hold the application lock here.
        var actual = Composition.Runtime.GetStatus(new CommunicationDeviceRef(id));
        lock (_gate)
        {
            if (!_connectionsReady)
                return new(false, CommunicationState.Disconnected, false, 0,
                    _disposed ? "已断开" : "正在切换方案", "");
            var device = _activeSolution?.Communications.Find(d => d.Id == id);
            var enabled = !_disposed && device?.IsEnabled == true && _connectionIntents.Contains(id) && !_manualDisconnects.Contains(id);
            var reconnecting = enabled && device!.AutoReconnect &&
                _reconnecting.TryGetValue(id, out var activeRetry) && !activeRetry.Cancellation.IsCancellationRequested;
            var waiting = reconnecting && _reconnecting.TryGetValue(id, out var retry) && retry.Waiting &&
                actual.State is not (CommunicationState.Connected or CommunicationState.Connecting or CommunicationState.Disconnecting);
            var seconds = 0;
            if (waiting && _reconnecting.TryGetValue(id, out var pending))
                seconds = Math.Max(0, (int)Math.Ceiling(Stopwatch.GetElapsedTime(
                    Stopwatch.GetTimestamp(), pending.RetryDueTimestamp).TotalSeconds));
            var reason = _connectionErrors.GetValueOrDefault(id, actual.LastError?.Message ?? "");
            var text = actual.State switch
            {
                CommunicationState.Connected => device?.IsTcpServer == true ? "监听中" :
                    device?.IsUdp == true ? "本地端口已绑定" : device?.IsPlainSerial == true ? "串口已打开" : "已连接",
                CommunicationState.Connecting => "连接中",
                CommunicationState.Disconnecting => "正在断开",
                _ when !enabled => device?.IsEnabled == true ? "已断开" : "设备已停用",
                _ when _reconnectFailures.GetValueOrDefault(id) >= MaxReconnectFailures =>
                    $"自动重连已停止（连续失败{MaxReconnectFailures}次）",
                _ when waiting => $"等待重连（{seconds} 秒）",
                _ when reconnecting => "正在重连（尚未连接）",
                CommunicationState.Faulted => "连接失败",
                _ => device?.AutoReconnect == true ? "已断开" : "已断开（自动重连未开启）"
            };
            return new(enabled, actual.State, waiting, seconds, text,
                enabled && actual.State != CommunicationState.Connected ? reason : "");
        }
    }
}
