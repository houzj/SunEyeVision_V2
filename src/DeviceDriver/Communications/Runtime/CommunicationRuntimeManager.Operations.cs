using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Devices.Communication;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.DeviceDriver.Communications;

public sealed partial class CommunicationRuntimeManager
{
    private T RequireReadySession<T>(string id, CommunicationStatusSnapshot? expected = null) where T : class, ICommunicationAdapter
    {
        // Reconcile an already-published connection before exposing it to a caller.
        _ = GetStatus(new CommunicationDeviceRef(id));
        lock (_stateGate)
        {
            if (expected != null && (expected.Device.Value != id || !IsCurrentStatus(expected)))
                throw new InvalidOperationException("提交前通讯会话已更换，本次操作未发送；请重新执行，不自动重发。");
            if (_statuses.TryGetValue(id, out var status) && status.State == CommunicationState.Connected &&
                _sessions.TryGetValue(id, out var active) && active is T session && session.IsConnected)
                return session;
        }
        throw new InvalidOperationException("通讯设备尚未就绪、正在重连或协议不匹配，请等待连接恢复后重新执行。");
    }

    private async Task<TValue> ExecuteConnectedAsync<TSession, TValue>(string id, Func<TSession, Task<TValue>> operation,
        CancellationToken cancellationToken = default, CommunicationStatusSnapshot? expected = null)
        where TSession : class, ICommunicationAdapter
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = RequireReadySession<TSession>(id, expected);
        try
        {
            var value = await operation(session).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_stateGate)
                if (!_sessions.TryGetValue(id, out var active) || !ReferenceEquals(active, session) || !session.IsConnected)
                    throw new InvalidOperationException("操作期间通讯连接已失效或更换，本次结果未采用，请重新执行。");
            return value;
        }
        catch (Exception ex)
        {
            // A listener with no clients and a UDP destination error do not invalidate
            // the local listener/binding. Business/protocol rejection is not a link fault.
            var broken = !session.IsConnected ||
                session is not TcpServerSession and not UdpSession && IsTransportFailure(ex);
            if (broken)
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"通讯操作发现连接不可用：设备={id}, 协议={session.GetType().Name}, 原因={ex.Message}",
                    "CommunicationRuntime", ex, LogTarget.File | LogTarget.UI);
                OnConnectionClosed(session, new CommunicationConnectionClosedEventArgs(id, ex.Message));
            }
            throw;
        }
    }

    private Task ExecuteConnectedAsync<TSession>(string id, Func<TSession, Task> operation,
        CancellationToken cancellationToken = default, CommunicationStatusSnapshot? expected = null)
        where TSession : class, ICommunicationAdapter =>
        ExecuteConnectedAsync<TSession, bool>(id, async session =>
        {
            await operation(session).ConfigureAwait(false);
            return true;
        }, cancellationToken, expected);

    private static bool IsTransportFailure(Exception error)
    {
        if (error is OperationCanceledException or ArgumentException or NotSupportedException or InvalidDataException) return false;
        // InvalidDataException may describe a complete, negative protocol response;
        // protocols that lose framing must explicitly invalidate their own adapter.
        if (error is IOException or SocketException or TimeoutException or ObjectDisposedException)
            return true;
        if (error is S7.Net.PlcException { ErrorCode: S7.Net.ErrorCode.ConnectionError }) return true;
        return error.InnerException != null && IsTransportFailure(error.InnerException);
    }
}
