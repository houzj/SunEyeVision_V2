using SunEyeVision.Domain.Solutions;

namespace SunEyeVision.Application;

/// <summary>The committed document identities, not display IDs or private editor instances.</summary>
public sealed record DocumentEditReceipt(DocumentSession Session, DocumentChangeSet? Change,
    DocumentHistoryEntry? History, IReadOnlyDictionary<GlobalVariable, GlobalVariable> AcceptedVariables);
