using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

/// <summary>Host-owned declarations and private proposal validation; never a modal picker or a value store.</summary>
public sealed record ReferenceFieldOptions(
    Func<Port,IReadOnlyList<DeclaredReferenceCandidate>> Candidates,
    Func<Port,ParameterSetting,string?>? ValidateProposal = null,
    bool EditableIndex = false,
    Action<ReferenceExpansionRequest>? Expand = null)
{
    public static ReferenceFieldOptions Empty { get; } = new(_ => Array.Empty<DeclaredReferenceCandidate>());
}

public sealed record ReferenceExpansionRequest(Port Target, bool IsIndex,
    IReadOnlyList<DeclaredReferenceCandidate> Candidates, Func<OutputReference,bool> Submit);
