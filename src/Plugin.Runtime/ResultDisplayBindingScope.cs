using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Runtime;

/// <summary>Presentation visibility is a frozen front/self source list. It creates no execution edges.</summary>
public sealed class ResultDisplayBindingScope : IBindingCompilationScope,IBindingReferenceQualification
{
    private readonly IReadOnlyDictionary<BindingNodeId,BindingSource> _sources;
    public ResultDisplayBindingScope(BindingNodeId consumer,Guid document,long revision,IEnumerable<BindingSource> sources)
    {Consumer=consumer;DocumentSessionId=document;DocumentRevision=revision;_sources=sources.ToDictionary(source=>source.Node);}
    public BindingNodeId Consumer{get;}
    public Guid DocumentSessionId{get;}
    public long DocumentRevision{get;}
    public bool TryGetSource(BindingNodeId id,out BindingSource source)=>_sources.TryGetValue(id,out source!);
    public bool TryGetVariable(VariableReference reference,out BindingVariableSource source){source=null!;return false;}
    public Diagnostic? ValidateDependencies(IReadOnlyList<BindingNodeId> sources)=>sources.Any(id=>!_sources.ContainsKey(id))?new("display-scope","显示来源不属于已冻结的前驱或当前节点。"):null;
    public Diagnostic? ValidateReference(OutputReference reference)=>reference.ScopeId is {} scope&&scope!=Consumer.ScopeId?new("display-scope","显示来源不属于当前流程。"):
        HasVariable(reference.Selection)?new("display-index","显示来源暂不支持变量索引。"):null;
    private static bool HasVariable(BindingSelection selection)=>selection is BindingSelection.AtVariable
        ||selection is BindingSelection.AtField index&&HasVariable(index.Reference.Selection);
}
