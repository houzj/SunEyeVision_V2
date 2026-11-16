using System.Threading;
using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>临时生命周期计数探针。用于确认重复执行不会累积运行态资源。</summary>
public static class RuntimeLifecycleProbe
{
    private static int _batches, _engines, _sessions, _resultOwners, _executionServers, _executionRequests;
    public static IDisposable EnterBatch() => Enter(() => Interlocked.Increment(ref _batches), () => Interlocked.Decrement(ref _batches));
    public static IDisposable EnterEngine() => Enter(() => Interlocked.Increment(ref _engines), () => Interlocked.Decrement(ref _engines));
    public static IDisposable EnterSession() => Enter(() => Interlocked.Increment(ref _sessions), () => Interlocked.Decrement(ref _sessions));
    public static IDisposable EnterResultOwner() => Enter(() => Interlocked.Increment(ref _resultOwners), () => Interlocked.Decrement(ref _resultOwners));
    public static IDisposable EnterExecutionServer() => Enter(() => Interlocked.Increment(ref _executionServers), () => Interlocked.Decrement(ref _executionServers));
    public static IDisposable EnterExecutionRequest() => Enter(() => Interlocked.Increment(ref _executionRequests), () => Interlocked.Decrement(ref _executionRequests));
    public static string Snapshot() => $"batches={Volatile.Read(ref _batches)}/engines={Volatile.Read(ref _engines)}/sessions={Volatile.Read(ref _sessions)}/resultOwners={Volatile.Read(ref _resultOwners)}/executionServers={Volatile.Read(ref _executionServers)}/executionRequests={Volatile.Read(ref _executionRequests)}";
    public static void Log(string stage) => PluginLogger.Info($"[LifecycleProbe] {stage} {Snapshot()}", "ExecutionLifecycle");
    private static IDisposable Enter(Action increment, Action decrement)
    {
        increment();
        return new Lease(decrement);
    }
    private sealed class Lease(Action release) : IDisposable
    { private Action? _release = release; public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke(); }
}
