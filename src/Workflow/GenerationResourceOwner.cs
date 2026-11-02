using System;
using System.Collections.Generic;
using System.Threading;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Workflow;

/// <summary>Transfers one invocation's resource scope and directory lease to its result owner.</summary>
internal sealed class GenerationResourceOwner(IDisposable resources,RuntimeGenerationLease generation):IDisposable
{
    private sealed record Owned(IDisposable Resources,RuntimeGenerationLease Generation);
    private Owned? _owned=new(resources,generation);
    public void Dispose()
    {
        var owned=Interlocked.Exchange(ref _owned,null);
        if(owned is null)return;
        var failures=new List<Exception>();
        try{owned.Resources.Dispose();}catch(Exception e){failures.Add(e);}
        try{owned.Generation.Dispose();}catch(Exception e){failures.Add(e);}
        if(failures.Count!=0)throw new AggregateException("Runtime resources and generation cleanup failed.",failures);
    }
}
