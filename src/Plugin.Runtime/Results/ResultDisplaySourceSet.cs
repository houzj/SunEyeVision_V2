using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>Explicit frozen source set. Holds value-only readers, never another display set,
/// so a source graph cannot create recursive result leases.</summary>
public sealed class ResultDisplaySourceSet : IDisposable
{
    private readonly IReadOnlyDictionary<BindingNodeId,DeclaredOutputReader> _sources;
    private readonly ResultResourceOwner _owner;
    public ResultDisplaySourceSet(IEnumerable<DeclaredOutputSnapshot> sources)
    {
        var readers=new Dictionary<BindingNodeId,DeclaredOutputReader>();
        try
        {
            foreach(var source in sources)
            {
                if(source.SourceWorkflowId is not {Length:>0} scope)throw new ArgumentException("Display source requires an explicit workflow identity.");
                var key=new BindingNodeId(scope,source.Identity.NodeId);
                if(readers.ContainsKey(key))throw new ArgumentException("Display source identities must be unique.");
                readers.Add(key,source.AcquireValueReader());
            }
            _sources=new System.Collections.ObjectModel.ReadOnlyDictionary<BindingNodeId,DeclaredOutputReader>(readers);
            _owner=new ResultResourceOwner(()=>Release(readers.Values));
        }
        catch(Exception failure)
        {try{Release(readers.Values);}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    public IReadOnlyList<RunIdentity> Identities=>Array.AsReadOnly(_sources.Values.Select(reader=>reader.Snapshot.Identity).ToArray());
    public ResultDisplaySourceLease Acquire()=>new(_sources,_owner.Retain());
    public void Dispose()=>_owner.Dispose();
    private static void Release(IEnumerable<IDisposable> values)
    {var failures=new List<Exception>();foreach(var value in values)try{value.Dispose();}catch(Exception failure){failures.Add(failure);}if(failures.Count!=0)throw new AggregateException("Display source readers failed to release.",failures);}
}

/// <summary>Borrowed only while its owning result reader or explicit lease is alive.</summary>
public sealed class ResultDisplaySourceLease : IDisposable
{
    private readonly IReadOnlyDictionary<BindingNodeId,DeclaredOutputReader> _sources;
    private IDisposable? _owner;
    internal ResultDisplaySourceLease(IReadOnlyDictionary<BindingNodeId,DeclaredOutputReader> sources,IDisposable owner)
    {_sources=sources;_owner=owner;}
    public IReadOnlyList<BindingNodeResult> CaptureResults()
    {
        ObjectDisposedException.ThrowIf(_owner is null,this);
        return Array.AsReadOnly(_sources.Select(pair=>new BindingNodeResult(pair.Key,pair.Value.Snapshot.Identity,
            pair.Value.Snapshot.SchemaRevision,pair.Value.Snapshot.Schema.Fingerprint,pair.Value.Snapshot.IsSuccess,
            pair.Value.Values??new InputValues(Array.Empty<Port>(),new Dictionary<string,object?>()))).ToArray());
    }
    public IReadOnlyList<BindingSource> CaptureDeclarations()
    {
        ObjectDisposedException.ThrowIf(_owner is null,this);
        return Array.AsReadOnly(_sources.Select(pair=>new BindingSource(pair.Key,pair.Value.Snapshot.Identity.ScopeId,
            pair.Value.Snapshot.Schema,pair.Value.Snapshot.SchemaRevision)).ToArray());
    }
    public bool TryGet(BindingNodeId id,out DeclaredOutputSnapshot? snapshot,out InputValues? values)
    {
        ObjectDisposedException.ThrowIf(_owner is null,this);
        if(_sources.TryGetValue(id,out var reader)){snapshot=reader.Snapshot;values=reader.Values;return true;}
        snapshot=null;values=null;return false;
    }
    public void Dispose()=>Interlocked.Exchange(ref _owner,null)?.Dispose();
}
