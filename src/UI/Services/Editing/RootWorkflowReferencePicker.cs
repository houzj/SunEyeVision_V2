using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Wpf.Adapters.References;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>The scoped reference picker remains unavailable until the session supplies admitted candidates.</summary>
internal static class RootWorkflowReferencePicker
{
 internal static ReferenceFieldOptions Options(RootWorkflowConfigurationContext context, RootParameterProxy proxy) =>
  new(_ => ReferenceCandidates.Empty, (_, _) => "正式引用来源选择尚未接入。");
}
