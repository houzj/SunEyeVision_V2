using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Workflow.Persistence;

public sealed record PersistenceDiagnostic(string Code, string Stage, string Severity, string JsonPath,
    string Message, string? WorkflowId = null, string? NodeId = null, string? VariableId = null, string? Migration = null);

public sealed record SolutionLoadResult(Solution? Candidate, string? SourceSha256, string? BaseDirectory,
    IReadOnlyList<PersistenceDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(d => d.Severity == "Error");
}
