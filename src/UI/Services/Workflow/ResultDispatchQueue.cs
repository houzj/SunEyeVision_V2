using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.UI.Services.Workflow;

/// <summary>Only presentation is coalesced. Execution and stored run results are unaffected.
/// There is one posted drain and at most one unclaimed result per node.</summary>
internal sealed class ResultDispatchQueue(Action<Action> post) : IDisposable
{
    private sealed class Pending(string key, Action publish)
    {
        internal readonly string Key=key;
        internal readonly Action Publish=publish;
        internal ResultDispatchLease Lease=null!;
    }
    private readonly object _gate=new();
    private readonly Dictionary<string,Pending> _pending=new(StringComparer.Ordinal);
    private readonly Dictionary<string,Pending> _draining=new(StringComparer.Ordinal);
    private bool _scheduled, _disposed;
    private long _ticket;

    internal void Enqueue(string key, DeclaredOutputSnapshot result, Action publish)
    {
        if(result.ExecutionIdentity?.IsCurrent==false)return;
        var item=new Pending(key,publish);
        item.Lease=new ResultDispatchLease(result,()=>Remove(item));
        Pending? previous=null, draining=null;
        long ticket=0;
        bool accepted=false;
        lock(_gate)
        {
            if(!_disposed&&item.Lease.IsPending)
            {
                accepted=true;
                _pending.Remove(key,out previous);
                _draining.TryGetValue(key,out draining);
                _pending.Add(key,item);
                if(!_scheduled){_scheduled=true;ticket=++_ticket;}
            }
        }
        var errors=new List<Exception>();
        if(!accepted)TryDispose(item,errors);
        if(previous is not null)TryDispose(previous,errors);
        if(draining is not null)TryDispose(draining,errors);
        if(ticket!=0)Schedule(ticket,errors);
        ThrowErrors(errors);
    }
    private void Remove(Pending item)
    {
        lock(_gate)
        {
            if(_pending.TryGetValue(item.Key,out var current)&&ReferenceEquals(current,item))_pending.Remove(item.Key);
        }
    }
    private void Schedule(long ticket,List<Exception> errors)
    {
        try{post(()=>Drain(ticket));}
        catch(Exception error)
        {
            errors.Add(error);
            Pending[] abandoned;
            lock(_gate)
            {
                if(_ticket!=ticket)return;
                _scheduled=false;abandoned=_pending.Values.ToArray();_pending.Clear();
            }
            foreach(var item in abandoned)TryDispose(item,errors);
        }
    }
    private void Drain(long ticket)
    {
        Pending[] batch;
        lock(_gate)
        {
            if(_disposed||ticket!=_ticket)return;
            batch=_pending.Values.ToArray();_pending.Clear();
            foreach(var item in batch)_draining.Add(item.Key,item);
        }
        var errors=new List<Exception>();
        foreach(var item in batch)
        {
            try
            {
                using var lease=item.Lease.Claim();
                if(lease is not null)item.Publish();
            }
            catch(Exception error){errors.Add(error);}
            finally{TryDispose(item,errors);}
        }
        long next=0;
        lock(_gate)
        {
            _draining.Clear();
            if(!_disposed&&_pending.Count>0)next=++_ticket;
            else _scheduled=false;
        }
        if(next!=0)Schedule(next,errors);
        ThrowErrors(errors);
    }
    public void Dispose()
    {
        Pending[] abandoned;
        lock(_gate)
        {
            if(_disposed)return;
            _disposed=true;++_ticket;_scheduled=false;
            abandoned=_pending.Values.Concat(_draining.Values).ToArray();
            _pending.Clear();_draining.Clear();
        }
        var errors=new List<Exception>();
        foreach(var item in abandoned)TryDispose(item,errors);
        ThrowErrors(errors);
    }
    private static void TryDispose(Pending item,List<Exception> errors)
    {try{item.Lease.Dispose();}catch(Exception error){errors.Add(error);}}
    private static void ThrowErrors(List<Exception> errors)
    {if(errors.Count>0)throw new AggregateException("结果通知呈现或清理失败；其余通知已尝试处理。",errors);}
}
