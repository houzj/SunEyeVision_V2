using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

/// <summary>Host-owned declarations and private proposal validation; never a modal picker or a value store.</summary>
public sealed record ReferenceFieldOptions(
    Func<Port,ReferenceCandidates> ReadCandidates,
    Func<Port,ParameterSetting,string?>? ValidateProposal = null,
    bool EditableIndex = false,
    Action<ReferenceExpansionRequest>? Expand = null,
    Action<VariableSelectionRequest>? SelectVariable=null,
    Func<VariableReference,string>? FormatVariable=null,
    Func<Port,ConstantSetting?>? ReadCurrentValue=null,
    ReferenceWindowOwner? WindowOwner=null,
    Func<Port,ParameterReferenceBehavior>? ReadBehavior=null)
{
    public static ReferenceFieldOptions Empty { get; } = new(_ => ReferenceCandidates.Empty);
}
