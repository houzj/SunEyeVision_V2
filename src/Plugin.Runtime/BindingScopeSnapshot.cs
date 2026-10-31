using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>Frozen declarations, visibility and execution predecessors supplied by the owning workflow graph.</summary>
public sealed class BindingScopeSnapshot:IBindingCompilationScope,IBindingReferenceQualification
{
    private readonly Func<OutputReference,Diagnostic?>? _qualification;
    private readonly Dictionary<BindingNodeId,BindingSource> _sources;
    private readonly HashSet<BindingNodeId> _visible;
    private readonly Dictionary<BindingNodeId,BindingNodeId[]> _predecessors;
    private readonly Dictionary<(string Solution,string Variable),BindingVariableSource> _variables;
    public BindingScopeSnapshot(BindingNodeId consumer,Guid documentSessionId,long documentRevision,
        IEnumerable<BindingSource> sources,IEnumerable<BindingNodeId> visibleSources,
        IEnumerable<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>> predecessors,IEnumerable<BindingVariableSource>? variables=null,Func<OutputReference,Diagnostic?>? qualification=null)
    {
        Consumer=consumer;DocumentSessionId=documentSessionId;DocumentRevision=documentRevision;_qualification=qualification;
        _sources=sources.ToDictionary(source=>source.Node);
        _visible=new(visibleSources);
        _predecessors=predecessors.ToDictionary(pair=>pair.Key,pair=>pair.Value.ToArray());
        _variables=(variables??Array.Empty<BindingVariableSource>()).ToDictionary(value=>(value.Reference.SolutionId,value.Reference.VariableId));
    }
    public BindingScopeSnapshot WithReferenceQualification(Func<OutputReference,Diagnostic?> qualification)
    {
        ArgumentNullException.ThrowIfNull(qualification);
        return new(Consumer,DocumentSessionId,DocumentRevision,_sources.Values,_visible,
            _predecessors.Select(pair=>new KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>(pair.Key,pair.Value)),
            _variables.Values,qualification);
    }
    public Diagnostic? ValidateReference(OutputReference reference)=>_qualification?.Invoke(reference);
    public BindingNodeId Consumer {get;}
    public Guid DocumentSessionId {get;}
    public long DocumentRevision {get;}
    public bool TryGetVariable(VariableReference reference,out BindingVariableSource source)
        =>_variables.TryGetValue((reference.SolutionId,reference.VariableId),out source!);
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
    private readonly Dictionary<(string Solution,string Variable),BindingVariableValue> _variables;
    public BindingRunSnapshot(BindingNodeId consumer,RunIdentity identity,long documentRevision,IEnumerable<BindingNodeResult> results,
        IReadOnlyDictionary<BindingNodeId,RunIdentity> expectedSourceIdentities,IEnumerable<BindingVariableValue>? variables=null)
    {
        Consumer=consumer;ConsumerIdentity=identity;DocumentRevision=documentRevision;
        _results=results.ToDictionary(result=>result.Node);
        // Expected identities come from admission, independently of the result candidates being validated.
        _expected=expectedSourceIdentities.ToDictionary(pair=>pair.Key,pair=>pair.Value);
        _variables=(variables??Array.Empty<BindingVariableValue>()).Select(value=>value with {
            Value=value.Issue is null?ValueRules.Freeze(new Port(value.Source.Reference.VariableId,value.Source.Reference.Type,Storage:value.Source.Reference.Storage),value.Value):null})
            .ToDictionary(value=>(value.Source.Reference.SolutionId,value.Source.Reference.VariableId));
    }
    public BindingNodeId Consumer {get;}
    public RunIdentity ConsumerIdentity {get;}
    public long DocumentRevision {get;}
    public bool TryGetExpectedSourceIdentity(BindingNodeId node,out RunIdentity identity)=>_expected.TryGetValue(node,out identity);
    public bool TryGetResult(BindingNodeId node,out BindingNodeResult result)=>_results.TryGetValue(node,out result!);
    public bool TryGetVariable(VariableReference reference,out BindingVariableValue value)
        =>_variables.TryGetValue((reference.SolutionId,reference.VariableId),out value!);
}

/// <summary>Explicit preview inputs selected from already published invocations.
/// Source identities stay original; this is never a workflow run snapshot. The host
/// must retain the selected output readers for the entire preview invocation.</summary>
public sealed class PublishedBindingSnapshot : IBindingRunSnapshot
{
    private readonly BindingRunSnapshot _snapshot;
    public PublishedBindingSnapshot(BindingNodeId consumer,RunIdentity identity,long revision,IEnumerable<BindingNodeResult> sources,
        IEnumerable<BindingVariableValue>? variables=null)
    {
        var frozen=sources.ToArray();
        if(identity.RequestId==Guid.Empty||identity.NodeId!=consumer.NodeId||frozen.Any(s=>s.Node.ScopeId!=consumer.ScopeId||s.Node==consumer||
            s.Identity.NodeId!=s.Node.NodeId||s.Identity.RequestId==Guid.Empty||s.Identity.ScopeId==Guid.Empty))
            throw new ArgumentException("Published input identities do not belong to the captured preview scope.");
        _snapshot=new(consumer,identity,revision,frozen,frozen.ToDictionary(s=>s.Node,s=>s.Identity),variables);
    }
    public RunIdentity ConsumerIdentity=>_snapshot.ConsumerIdentity;
    public BindingNodeId Consumer=>_snapshot.Consumer;
    public long DocumentRevision=>_snapshot.DocumentRevision;
    public bool TryGetExpectedSourceIdentity(BindingNodeId node,out RunIdentity identity)=>_snapshot.TryGetExpectedSourceIdentity(node,out identity);
    public bool TryGetResult(BindingNodeId node,out BindingNodeResult result)=>_snapshot.TryGetResult(node,out result!);
    public bool TryGetVariable(VariableReference reference,out BindingVariableValue value)=>_snapshot.TryGetVariable(reference,out value!);
}
