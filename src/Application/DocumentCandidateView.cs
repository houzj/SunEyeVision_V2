using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

// A single private candidate used by binding, dependency and edit validation.
// Flow is either the actual session member or an identity issued by this edit;
// neither case is a new live session scope.
internal sealed class DocumentCandidateView
{
    internal sealed record Flow(WorkflowDefinition Identity, string Name, GraphState Graph,
        RootWorkflowConfiguration RootConfiguration, RootWorkspaceSettings Workspace);

    internal DocumentCandidateView(IEnumerable<Flow> flows, IEnumerable<GlobalVariable> variables)
    {
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(variables);
        var ordered = flows.ToArray();
        if (ordered.Select(item => item.Identity).Distinct(ReferenceEqualityComparer.Instance).Count() != ordered.Length ||
            ordered.Select(item => item.Identity.Id).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new InvalidOperationException("候选流程身份不唯一。");
        Flows = Array.AsReadOnly(ordered);
        Variables = Array.AsReadOnly(variables.ToArray());
    }

    internal IReadOnlyList<Flow> Flows { get; }
    internal IReadOnlyList<GlobalVariable> Variables { get; }
}
