using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.UI.Services.Workflow;

/// <summary>A pending notification retains its exact result. Cancellation or owner retirement
/// releases an unclaimed notification without waiting for a UI message pump.</summary>
internal sealed class ResultDispatchLease : IDisposable
{
    private readonly object _gate=new();
    private readonly ToolExecutionIdentity? _identity;
    private readonly string? _domain;
    private IDisposable? _pending;
    private CancellationTokenRegistration _cancellation;
    private bool _closed;
    private readonly Action? _onClosed;
    internal bool IsPending { get { lock(_gate)return !_closed; } }

    internal ResultDispatchLease(DeclaredOutputSnapshot result):this(result,null) { }
    internal ResultDispatchLease(DeclaredOutputSnapshot result, Action? onClosed)
    {
        _onClosed=onClosed;
        _identity=result.ExecutionIdentity;
        _domain=_identity?.Domain;
        _pending=result.Retain();
        try
        {
            if(_identity is not null)
            {
                _identity.Owner.DomainRetired+=OnRetired;
                _cancellation=_identity.CancellationToken.Register(Dispose);
                bool closed;lock(_gate)closed=_closed;
                if(closed)_cancellation.Unregister();
                if(!_identity.IsCurrent)Dispose();
            }
        }
        catch{Dispose();throw;}
    }
    private void OnRetired(string domain){if(domain==_domain)Dispose();}
    internal IDisposable? Claim()
    {
        IDisposable? lease;
        lock(_gate){if(_closed)return null;_closed=true;lease=_pending;_pending=null;}
        Detach();return lease;
    }
    private void Detach()
    {
        if(_identity is not null)_identity.Owner.DomainRetired-=OnRetired;
        _cancellation.Unregister();
        _onClosed?.Invoke();
    }
    public void Dispose()
    {
        IDisposable? lease;
        lock(_gate){if(_closed)return;_closed=true;lease=_pending;_pending=null;}
        Detach();lease?.Dispose();
    }
}
