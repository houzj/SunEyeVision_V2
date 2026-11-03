using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.UI.Services.Workflow;

/// <summary>Explicitly protects the UI until the application exposes a non-mutating readiness query.</summary>
internal static class RootWorkflowReadiness
{
 internal static IReadOnlyList<string> Evaluate(DocumentSession session, WorkflowDefinition root)
 {
  ArgumentNullException.ThrowIfNull(session); ArgumentNullException.ThrowIfNull(root);
  if (root.IsSubroutine || !session.GetWorkflows().Any(item => ReferenceEquals(item, root)))
   return new[] { "根流程不属于当前方案会话。" };
  return new[] { "流程运行就绪检查尚未接入正式会话。" };
 }
}
