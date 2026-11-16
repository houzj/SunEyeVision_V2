using System.Diagnostics;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>临时 Blob 分段耗时探针，采证完成后删除。</summary>
internal sealed class BlobTimingProbe : IDisposable
{
    private static readonly bool Enabled = string.Equals(
        Environment.GetEnvironmentVariable("SUNEYEVISION_TIMING_PROBE"), "1", StringComparison.Ordinal);
    private static readonly BlobTimingProbe Disabled = new(null,null,false);
    private readonly Action<string>? _log; private readonly string? _node; private readonly Stopwatch? _clock;
    private readonly List<(string Stage,long Ms)>? _stages; private readonly bool _enabled; private long _last; private bool _disposed;
    private BlobTimingProbe(Action<string>? log,string? node,bool enabled)
    {
        _log=log;_node=node;_enabled=enabled;
        if(enabled){_clock=Stopwatch.StartNew();_stages=new();}
    }
    public static BlobTimingProbe Begin(Action<string> log,string node)=>Enabled?new(log,node,true):Disabled;
    public void Mark(string stage){if(!_enabled||_disposed)return;var now=_clock!.ElapsedMilliseconds;_stages!.Add((stage,now-_last));_last=now;}
    public void Dispose(){if(!_enabled||_disposed)return;_disposed=true;_clock!.Stop();_log!($"[ExecTimingProbe] tool=blob node={_node} total={_clock.ElapsedMilliseconds}ms stages={string.Join("/",_stages!.Select(x=>x.Stage+"="+x.Ms+"ms"))}");}
}
