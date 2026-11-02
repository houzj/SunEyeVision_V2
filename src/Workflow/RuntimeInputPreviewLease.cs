using System;
using System.Collections.Generic;
using System.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
namespace SunEyeVision.Workflow;

/// <summary>One read-only resolved input snapshot; all borrowed publication readers stay owned until disposal.</summary>
public sealed class RuntimeInputPreviewLease : IDisposable
{
    private Action? _release;
    private readonly Func<bool> _current;
    public InputValues Inputs {get;}
    public IReadOnlyList<RunIdentity> SourceIdentities {get;}
    private bool _checking;
    public bool IsCurrent
    {
        get
        {
            var release=_release;if(release is null||_checking)return false;_checking=true;
            try{var valid=_current()&&ReferenceEquals(release,_release);if(!valid)Dispose();return valid;}
            catch(Exception failure){try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}return false;}
            finally{_checking=false;}
        }
    }
    internal RuntimeInputPreviewLease(InputValues inputs,IReadOnlyList<RunIdentity> sources,Func<bool> current,Action release)
    {Inputs=inputs;SourceIdentities=sources;_current=current;_release=release;}
    public void Dispose()=>Interlocked.Exchange(ref _release,null)?.Invoke();
}
