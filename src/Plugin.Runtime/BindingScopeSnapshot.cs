using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>Frozen declarations, visibility and execution predecessors supplied by the owning workflow graph.</summary>
public sealed class BindingScopeSnapshot:IBindingCompilationScope
{
    private readonly Dictionary<BindingNodeId,BindingSource> _sources;
    private readonly HashSet<BindingNodeId> _visible;
    private readonly Dictionary<BindingNodeId,BindingNodeId[]> _predecessors;
    public BindingScopeSnapshot(BindingNodeId consumer,Guid documentSessionId,long documentRevision,
        IEnumerable<BindingSource> sources,IEnumerable<BindingNodeId> visibleSources,
        IEnumerable<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>> predecessors)
    {
        Consumer=consumer;DocumentSessionId=documentSessionId;DocumentRevision=documentRevision;
        _sources=sources.ToDictionary(source=>source.Node);
        _visible=new(visibleSources);
        _predecessors=predecessors.ToDictionary(pair=>pair.Key,pair=>pair.Value.ToArray());
    }
    public BindingNodeId Consumer {get;}
    public Guid DocumentSessionId {get;}
    public long DocumentRevision {get;}
    public bool TryGetSource(BindingNodeId node,out BindingSource source)
    {
        source=null!;
        return _visible.Contains(node)&&_sources.TryGetValue(node,out source!);
    }
    public Diagnostic? ValidateDependencies(IReadOnlyList<BindingNodeId> sources)
    {
        foreach(var source in sources)
        {
            if(!_visible.Contains(source))return new("scope","Source is outside the consumer's visible scope.");
            var pending=new Stack<BindingNodeId>();var seen=new HashSet<BindingNodeId>();pending.Push(source);
            while(pending.Count>0)
            {
                var node=pending.Pop();if(node==Consumer)return new("cycle","Binding introduces an execution dependency cycle.");
                if(!seen.Add(node))continue;
                if(_predecessors.TryGetValue(node,out var previous))foreach(var predecessor in previous)pending.Push(predecessor);
            }
        }
        return null;
    }
}

/// <summary>
/// Frozen successful and failed per-node result metadata for one consumer invocation.
/// This view does not acquire resource ownership. The execution owner keeps results alive until input leases are acquired.
/// </summary>
public sealed class BindingRunSnapshot:IBindingRunSnapshot
{
    private readonly Dictionary<BindingNodeId,BindingNodeResult> _results;
    private readonly Dictionary<BindingNodeId,RunIdentity> _expected;
    public BindingRunSnapshot(BindingNodeId consumer,RunIdentity identity,long documentRevision,IEnumerable<BindingNodeResult> results,
        IReadOnlyDictionary<BindingNodeId,RunIdentity> expectedSourceIdentities)
    {
        Consumer=consumer;ConsumerIdentity=identity;DocumentRevision=documentRevision;
        _results=results.ToDictionary(result=>result.Node);
        // Expected identities come from admission, independently of the result candidates being validated.
        _expected=expectedSourceIdentities.ToDictionary(pair=>pair.Key,pair=>pair.Value);
    }
    public BindingNodeId Consumer {get;}
    public RunIdentity ConsumerIdentity {get;}
    public long DocumentRevision {get;}
    public bool TryGetExpectedSourceIdentity(BindingNodeId node,out RunIdentity identity)=>_expected.TryGetValue(node,out identity);
    public bool TryGetResult(BindingNodeId node,out BindingNodeResult result)=>_results.TryGetValue(node,out result!);
}
