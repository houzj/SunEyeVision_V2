using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.Tool.Script.ScriptEngine;

/// <summary>所有编译入口共用同键任务。等待者取消只退出自身，已开始Emit允许完成并填充缓存。</summary>
internal static class ScriptCompilationCoordinator
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<ScriptCompiler.CompileResult>>> Jobs = new();
    private static readonly SemaphoreSlim CompileSlot = new(1, 1);
    internal static long EmitOperations;
    internal static readonly ConcurrentDictionary<string, int> EmissionCounts = new();
    internal static int GetEmitCount(string key) => EmissionCounts.TryGetValue(key, out var count) ? count : 0;
    public static async Task<ScriptCompiler.CompileResult> GetAsync(string key,
        Func<ScriptCompiler.CompileResult> compile, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var own = new Lazy<Task<ScriptCompiler.CompileResult>>(() => Task.Run(async () =>
        {
            await CompileSlot.WaitAsync().ConfigureAwait(false);
            try { return compile(); }
            finally { CompileSlot.Release(); }
        }), LazyThreadSafetyMode.ExecutionAndPublication);
        var shared = Jobs.GetOrAdd(key, own);
        var task = shared.Value;
        _ = task.ContinueWith(_ => ((ICollection<KeyValuePair<string, Lazy<Task<ScriptCompiler.CompileResult>>>>)Jobs)
            .Remove(new(key, shared)), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        var wait = Stopwatch.StartNew();
        var result = ScriptCompiler.Copy(await task.WaitAsync(token).ConfigureAwait(false));
        result.JoinedInFlight = !ReferenceEquals(own, shared);
        result.WaitMs = wait.Elapsed.TotalMilliseconds;
        return result;
    }
}
