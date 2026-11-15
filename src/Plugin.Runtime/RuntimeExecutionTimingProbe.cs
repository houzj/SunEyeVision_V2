using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>
/// 临时执行耗时探针。仅在 SUNEYE_EXEC_TIMING_PROBE=1 时输出，采证完成后删除。
/// 不参与执行决策，不改变会话、资源或结果生命周期。
/// </summary>
public sealed class RuntimeExecutionTimingProbe : IDisposable
{
    private static readonly bool Enabled = true; // TEMP-PROBE：采证期默认开启，采证后恢复环境变量开关
    private readonly string _tool;
    private readonly string _invocation;
    private readonly Stopwatch _clock;
    private readonly List<(string Stage, long Ms)> _stages = new();
    private long _last;
    private bool _disposed;

    private RuntimeExecutionTimingProbe(string tool, string invocation)
    {
        _tool = tool;
        _invocation = invocation;
        _clock = Stopwatch.StartNew();
    }

    public static RuntimeExecutionTimingProbe Begin(string tool, string invocation)
        => new(tool, invocation);

    public void Mark(string stage)
    {
        if (!Enabled || _disposed) return;
        var now = _clock.ElapsedMilliseconds;
        _stages.Add((stage, now - _last));
        _last = now;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!Enabled) return;
        _clock.Stop();
        var stages = string.Join("/", _stages.Select(item => item.Stage + "=" + item.Ms + "ms"));
        PluginLogger.Info($"[ExecTimingProbe] tool={_tool} invocation={_invocation} total={_clock.ElapsedMilliseconds}ms stages={stages}", "ExecutionTimingProbe");
    }
}
