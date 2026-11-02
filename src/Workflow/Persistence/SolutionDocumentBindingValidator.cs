using System.Linq;
using System.Text.Json;

namespace SunEyeVision.Workflow.Persistence;

/// <summary>Validates current Runtime bindings without normalizing legacy identities.</summary>
internal static class SolutionDocumentBindingValidator
{
    internal static void Validate(Solution candidate, bool validateAdmission = false)
    {
        foreach (var workflow in candidate.Workflows)
        foreach (var node in workflow.Nodes)
        {
            if (node.RuntimeConfiguration is null) throw new JsonException("A current node requires an admitted Runtime configuration.");
            var issues = RuntimeInputBindings.Validate(workflow, node, workflow.Id, validateAdmission);
            if (issues.Count > 0) throw new JsonException(issues[0].Message);
        }
    }
}
