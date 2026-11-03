namespace SunEyeVision.Persistence;

/// <summary>Owns only resources explicitly created during one decode. Transfer exactly once after full admission.</summary>
public sealed class DecodedResourcesScope : IDisposable
{
    private readonly object _gate = new();
    private List<IDisposable>? _owned = new();
    private readonly HashSet<IDisposable> _identities = new(ReferenceEqualityComparer.Instance);
    internal void RegisterNew(IDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var ended = false;
        lock (_gate)
        {
            if(_owned is null) ended=true;
            else
            {
                if(!_identities.Add(resource)) throw new InvalidOperationException("Decoded resource was already registered.");
                _owned.Add(resource);
            }
        }
        if(ended)
        {
            resource.Dispose();
            throw new ObjectDisposedException(nameof(DecodedResourcesScope));
        }
    }
    public IDisposable Transfer()
    {
        lock (_gate)
        {
            var values=_owned ?? throw new InvalidOperationException("Decoded resource scope was already transferred or disposed.");
            _owned=null;_identities.Clear();return new OwnedResources(values);
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

