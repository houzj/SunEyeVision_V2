using SunEyeVision.Domain.Workflows;
using SunEyeVision.Domain.Solutions;

namespace SunEyeVision.Application;

/// <summary>The committed document identities, not display IDs or private editor instances.</summary>
public sealed record DocumentEditReceipt(DocumentSession Session, DocumentChangeSet? Change,
    DocumentHistoryEntry? History, IReadOnlyDictionary<GlobalVariable, GlobalVariable> AcceptedVariables,
    IReadOnlyDictionary<WorkflowDefinition, WorkflowDefinition> AcceptedRoots)
{
    internal IReadOnlySet<object>? AcceptedProposalIds { get; init; }
    internal IReadOnlyList<(WorkflowDefinition Flow, string Name)>? AcceptedRootSnapshot { get; init; }
}
