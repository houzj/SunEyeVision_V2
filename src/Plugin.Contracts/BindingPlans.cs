namespace SunEyeVision.Plugin.Contracts;

public abstract class BindingSelection
{
    private protected BindingSelection(){}
    public static BindingSelection All {get;}=new AllItems();
    public sealed class AllItems:BindingSelection {internal AllItems(){}}
    public sealed class AtConstant:BindingSelection
    {
        public AtConstant(int index){if(index<0)throw new ArgumentOutOfRangeException(nameof(index));Index=index;}
        public int Index {get;}
    }
    public sealed class AtField:BindingSelection
    {
        public AtField(OutputReference reference,ParameterReferenceBehavior behavior=ParameterReferenceBehavior.StrictExactlyOne)
        {ArgumentNullException.ThrowIfNull(reference);if(!ParameterReferenceRules.CanUseIndexSource(ParameterReferenceRules.SelectedType(reference),behavior))throw new ArgumentException("Index source must be an admitted integer declaration.");Reference=reference;ConstructionBehavior=behavior;}
        public OutputReference Reference {get;}
        public ParameterReferenceBehavior ConstructionBehavior {get;}
    }
    public sealed class AtVariable:BindingSelection
    {
        public AtVariable(VariableReference reference,ParameterReferenceBehavior behavior=ParameterReferenceBehavior.StrictExactlyOne)
        {
            ArgumentNullException.ThrowIfNull(reference);
            if(!ParameterReferenceRules.CanUseIndexSource(reference.Type,behavior)||reference.Selection is not AllItems)
                throw new ArgumentException("A variable index requires an integer scalar without a nested selection.");
            Reference=reference;
        }
        public VariableReference Reference {get;}
    }
}
public enum BindingCompatibility { Compatible, Conditional, Incompatible, Unresolved }
public enum BindingConversion { Automatic, Float64ToInt64Checked }
public readonly record struct BindingNodeId(string ScopeId,string NodeId);
public sealed record BindingSource(BindingNodeId Node,Guid ExecutionScopeId,ToolSchema Schema,long SchemaRevision);
public sealed record BindingVariableSource(VariableReference Reference,BindingNodeId? Producer=null);
public sealed record BindingVariableValue(BindingVariableSource Source,RunIdentity ConsumerIdentity,object? Value,Diagnostic? Issue=null);

/// <summary>Actual graph/scope owner supplies visibility and cycle checks; never infer them from last results.</summary>
public interface IBindingCompilationScope
{
    BindingNodeId Consumer {get;}
    Guid DocumentSessionId {get;}
    long DocumentRevision {get;}
    bool TryGetSource(BindingNodeId node,out BindingSource source);
    bool TryGetVariable(VariableReference reference,out BindingVariableSource source);
    Diagnostic? ValidateDependencies(IReadOnlyList<BindingNodeId> sources);
}
/// <summary>Optional owner policy applied to every recursively compiled reference.</summary>
public interface IBindingReferenceQualification
{
    Diagnostic? ValidateReference(OutputReference reference);
}
public sealed record BindingReferencePlan(OutputReference Reference,BindingSource Source,TypeRef SelectedType,BindingReferencePlan? Index,bool Conditional,NumericStorage? Storage=null,BindingVariableSource? VariableIndex=null);
/// <summary>Immutable compiled instructions. Execution belongs to the existing parameter resolution service.</summary>
public sealed class BindingPlan
{
    public BindingPlan(BindingNodeId consumer,Guid documentSessionId,long documentRevision,BindingReferencePlan source,Port target,BindingConversion conversion,IEnumerable<BindingNodeId> dependencies,ParameterReferenceBehavior behavior=ParameterReferenceBehavior.StrictExactlyOne)
    {ParameterReferenceRules.ValidateTarget(target,behavior);Behavior=behavior;Consumer=consumer;DocumentSessionId=documentSessionId;DocumentRevision=documentRevision;Source=source;Target=target;Conversion=conversion;Dependencies=Array.AsReadOnly(dependencies.Distinct().ToArray());}
    public ParameterReferenceBehavior Behavior {get;}
    public BindingNodeId Consumer {get;}
    public Guid DocumentSessionId {get;}
    public long DocumentRevision {get;}
    public BindingReferencePlan Source {get;}
    public Port Target {get;}
    public BindingConversion Conversion {get;}
    public IReadOnlyList<BindingNodeId> Dependencies {get;}
}
public sealed record BindingCompilation(BindingCompatibility Compatibility,BindingPlan? Plan,Diagnostic? Diagnostic);
public sealed record BindingNodeResult(BindingNodeId Node,RunIdentity Identity,long SchemaRevision,string SchemaFingerprint,bool Success,InputValues Values);
public interface IBindingRunSnapshot
{
    RunIdentity ConsumerIdentity {get;}
    BindingNodeId Consumer {get;}
    long DocumentRevision {get;}
    bool TryGetExpectedSourceIdentity(BindingNodeId node,out RunIdentity identity);
    bool TryGetResult(BindingNodeId node,out BindingNodeResult result);
    bool TryGetVariable(VariableReference reference,out BindingVariableValue value);
}
public sealed record BindingResolution(bool Success,object? Value,Diagnostic? Diagnostic);
public interface IBindingPlanCompiler
{
    BindingCompilation CompileBinding(OutputReference reference,Port target,IBindingCompilationScope scope,BindingConversion conversion=BindingConversion.Automatic,ParameterReferenceBehavior behavior=ParameterReferenceBehavior.StrictExactlyOne);
}
