using System.Diagnostics;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>临时 Blob 分段耗时探针，采证完成后删除。</summary>
internal sealed class BlobTimingProbe : IDisposable
{
    private static readonly bool Enabled=true; // TEMP-PROBE：采证期默认开启，采证后恢复环境变量开关
    private readonly Action<string> _log; private readonly string _node; private readonly Stopwatch _clock=Stopwatch.StartNew();
    private readonly List<(string Stage,long Ms)> _stages=new(); private long _last; private bool _disposed;
    private BlobTimingProbe(Action<string> log,string node){_log=log;_node=node;}
    public static BlobTimingProbe Begin(Action<string> log,string node)=>new(log,node);
    public void Mark(string stage){if(!Enabled||_disposed)return;var now=_clock.ElapsedMilliseconds;_stages.Add((stage,now-_last));_last=now;}
    public void Dispose(){if(_disposed)return;_disposed=true;if(!Enabled)return;_clock.Stop();_log($"[ExecTimingProbe] tool=blob node={_node} total={_clock.ElapsedMilliseconds}ms stages={string.Join("/",_stages.Select(x=>x.Stage+"="+x.Ms+"ms"))}");}
}
