using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.UI.Services;

public sealed record InstanceActivationRequest(string? SolutionPath, DateTimeOffset ExpiresAt);
public sealed record InstanceActivationResult(bool Accepted, string Message)
{
    public static InstanceActivationResult Delivered { get; } = new(true, "");
}

/// <summary>
/// One workspace per Windows user/session, independent of install directory/version.
/// A dedicated background thread owns the mutex, avoiding async/UI-thread ownership races.
/// IPC only transfers bounded activation requests; it never starts workflows or loads devices.
/// </summary>
public sealed class SingleInstanceCoordinator : IDisposable
{
    private const int MaxMessageBytes = 65536;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();
    private readonly ManualResetEventSlim _release;
    private readonly Thread _owner;
    private readonly Task _server;
    private int _disposed;
    private int _closing;

    public static string DefaultIdentity
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            using var process = Process.GetCurrentProcess();
            var sid = identity.User?.Value ?? Environment.UserName;
            var user = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sid)))[..16];
            return $"SunEyeVision.Instance.{user}.{process.SessionId}";
        }
    }

    private SingleInstanceCoordinator(string pipeName, Thread owner, ManualResetEventSlim release,
        Func<InstanceActivationRequest, Task<InstanceActivationResult>> handler)
    {
        _pipeName = pipeName;
        _owner = owner;
        _release = release;
        _server = ServeAsync(handler);
    }

    public static SingleInstanceCoordinator? TryStart(
        Func<InstanceActivationRequest, Task<InstanceActivationResult>> handler, string? identity = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var name = identity ?? DefaultIdentity;
        var release = new ManualResetEventSlim();
        using var ready = new ManualResetEventSlim();
        bool acquired = false;
        Exception? error = null;
        var owner = new Thread(() =>
        {
            try
            {
                using var mutex = new Mutex(false, @"Local\" + name);
                try { acquired = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { acquired = true; }
                ready.Set();
                if (!acquired) return;
                try { release.Wait(); }
                finally { mutex.ReleaseMutex(); }
            }
            catch (Exception ex) { error = ex; ready.Set(); }
        }) { IsBackground = true, Name = "SunEyeVision instance lifetime" };
        owner.Start();
        // Only waits for a local kernel mutex decision, never for another application.
        ready.Wait();
        if (error != null) { release.Dispose(); ExceptionDispatchInfo.Capture(error).Throw(); }
        if (!acquired) { owner.Join(); release.Dispose(); return null; }
        return new SingleInstanceCoordinator(name, owner, release, handler);
    }

    public void StopAccepting() => _stop.Cancel();
    public void BeginClosing() => Interlocked.Exchange(ref _closing, 1);

    private async Task ServeAsync(Func<InstanceActivationRequest, Task<InstanceActivationResult>> handler)
    {
        var token = _stop.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                // A client that never sends a full message must not monopolize activation.
                using var clientLimit = CancellationTokenSource.CreateLinkedTokenSource(token);
                clientLimit.CancelAfter(TimeSpan.FromSeconds(5));
                var request = await ReadAsync<InstanceActivationRequest>(pipe, clientLimit.Token).ConfigureAwait(false);
                var response = Volatile.Read(ref _closing) != 0
                    ? new InstanceActivationResult(false, "SunEyeVision 正在退出，请等待退出完成后再启动。")
                    : request.ExpiresAt < DateTimeOffset.UtcNow
                    ? new InstanceActivationResult(false, "启动请求已超时，请重试。")
                    : await handler(request).WaitAsync(clientLimit.Token).ConfigureAwait(false);
                await WriteAsync(pipe, response, clientLimit.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException or InvalidDataException)
            {
                // Broken/invalid clients cannot stop the primary instance listener.
            }
            catch (Exception)
            {
                // Keep listening; application callbacks return their own user-facing failures.
                await Task.Delay(100, token).ConfigureAwait(false);
            }
        }
    }

    public static async Task<InstanceActivationResult> SendAsync(string? solutionPath, string? identity = null,
        TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(4);
        var expiresAt = DateTimeOffset.UtcNow.Add(limit);
        using var cancellation = new CancellationTokenSource(limit);
        try
        {
            using var pipe = new NamedPipeClientStream(".", identity ?? DefaultIdentity, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(cancellation.Token).ConfigureAwait(false);
            // Permit this exact primary process to bring its window forward.
            if (GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(), out var primaryPid))
                AllowSetForegroundWindow(primaryPid);
            var request = new InstanceActivationRequest(solutionPath, expiresAt);
            await WriteAsync(pipe, request, cancellation.Token).ConfigureAwait(false);
            return await ReadAsync<InstanceActivationResult>(pipe, cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException or InvalidDataException)
        {
            return new InstanceActivationResult(false,
                "SunEyeVision 已有实例，但暂时无法响应（可能正在启动、退出或无响应）。请稍后重试；不会启动第二个实例。");
        }
    }

    private static async Task<T> ReadAsync<T>(Stream stream, CancellationToken token)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, token).ConfigureAwait(false);
        var count = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (count <= 0 || count > MaxMessageBytes) throw new InvalidDataException("Invalid activation message length.");
        var bytes = new byte[count];
        await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(bytes) ?? throw new InvalidDataException("Empty activation message.");
    }

    private static async Task WriteAsync<T>(Stream stream, T message, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        if (bytes.Length > MaxMessageBytes) throw new InvalidDataException("Activation request is too large.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, token).ConfigureAwait(false);
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    /// <summary>Tests/controlled hosts release explicitly; App retains the lock until process death.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        StopAccepting();
        try { _server.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        _release.Set();
        _owner.Join();
        _release.Dispose();
        _stop.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(IntPtr pipe, out uint serverProcessId);
    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
