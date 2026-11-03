using System.Runtime.ExceptionServices;

namespace SunEyeVision.Persistence;

/// <summary>Owns only resources explicitly created during one decode. Transfer exactly once after full admission.</summary>
public sealed class DecodedResourcesScope : IDisposable
{
    private readonly object _gate = new();
    private List<IDisposable>? _owned = new();
    private readonly HashSet<IDisposable> _identities = new(ReferenceEqualityComparer.Instance);
    private bool _decodeClaimed;
    private readonly bool _failRegistrationAfterAdd, _failOwnerConstruction;
    public DecodedResourcesScope() { }
    internal DecodedResourcesScope(bool failRegistrationAfterAdd, bool failOwnerConstruction)
        => (_failRegistrationAfterAdd,_failOwnerConstruction)=(failRegistrationAfterAdd,failOwnerConstruction);
    internal void ClaimDecode()
    {
        lock (_gate)
        {
            if (_owned is null || _decodeClaimed)
                throw new InvalidOperationException("A decode scope belongs to exactly one candidate.");
            _decodeClaimed = true;
        }
    }
    internal void ReleaseAfterFailure(Exception primary)
    {
        try { Dispose(); }
        catch (Exception cleanup) { throw new AggregateException("Decoding and resource cleanup both failed.", primary, cleanup); }
        ExceptionDispatchInfo.Capture(primary).Throw();
    }
    internal void RegisterNew(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        Exception? failure=null; bool alreadyOwned=false;
        lock (_gate)
        {
            if(_owned is null) failure=new ObjectDisposedException(nameof(DecodedResourcesScope));
            else if(_identities.Contains(resource)) { alreadyOwned=true;failure=new InvalidOperationException("Decoded resource was already registered."); }
            else
            {
                try
                {
                    _owned.Add(resource);
                    try
                    {
                        if(_failRegistrationAfterAdd) throw new InvalidOperationException("Injected registration failure.");
                        if(!_identities.Add(resource))throw new InvalidOperationException("Decoded resource was already registered.");
                    }
                    catch { _owned.RemoveAt(_owned.Count-1);_identities.Remove(resource);throw; }
                }
                catch(Exception error) { failure=error; }
            }
        }
        if(failure is null)return;
        if(!alreadyOwned)
        {
            try { resource.Dispose(); }
            catch(Exception cleanup) { throw new AggregateException("Decoded resource registration and cleanup failed.",failure,cleanup); }
        }
        ExceptionDispatchInfo.Capture(failure).Throw();
    }
    public IDisposable Transfer()
    {
        lock (_gate)
        {
            var values=_owned ?? throw new InvalidOperationException("Decoded resource scope was already transferred or disposed.");
            if(_failOwnerConstruction)throw new InvalidOperationException("Injected owner construction failure.");
            var owner=new OwnedResources(values);
            _owned=null;_identities.Clear();return owner;
        }
    }
    public void Dispose()
    {
        List<IDisposable>? values;
        lock(_gate){values=_owned;_owned=null;_identities.Clear();}
        if(values is not null)Release(values);
    }
    private static void Release(List<IDisposable> values)
    {
        List<Exception>? errors=null;
        for(var i=values.Count-1;i>=0;i--)try{values[i].Dispose();}catch(Exception error){(errors??=new()).Add(error);}
        if(errors is not null)throw new AggregateException("Some decoded resources could not be released.",errors);
    }
    private sealed class OwnedResources(List<IDisposable> values) : IDisposable
    {
        private List<IDisposable>? _values=values;
        public void Dispose(){var owned=Interlocked.Exchange(ref _values,null);if(owned is not null)Release(owned);}
    }
}
