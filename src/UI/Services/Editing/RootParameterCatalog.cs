using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Editing.Wpf.Execution.Parameters;

namespace SunEyeVision.UI.Services.Editing;

internal sealed record RootParameterCandidate(NodeDefinition Node, string FieldId, string StoragePath,
 string DisplayName, ConfigurationFieldAccessor? Field, string? Unavailable = null, RootResourceProxyBinding? Resource = null)
{
 public string Label => Node.DispName + " · " + DisplayName + (Unavailable is null ? "" : "（" + Unavailable + "）");
}

internal static class RootParameterCatalog
{
 internal static IReadOnlyList<RootParameterCandidate> Describe(NodeConfigurationEditDraft draft)
 {
  var displayIds = draft.DisplaySchema.Inputs.Select(field => field.Id).ToHashSet(StringComparer.Ordinal);
  var fields = draft.InputSchema.Inputs.Select(field => (Port: field, Schema: draft.InputSchema))
   .Concat(draft.SettingsSchema.Inputs.Select(field => (Port: field, Schema: draft.SettingsSchema)))
   .Where(item => !displayIds.Contains(item.Port.Id) && item.Port.EditModes != FieldEditModes.None).ToArray();
  if (fields.Select(item => item.Port.Id).Distinct(StringComparer.Ordinal).Count() != fields.Length)
   throw new InvalidOperationException("工具声明包含重复的运行参数身份。");
  return fields.Select(item =>
  {
   var port = item.Port;
   var presentation = item.Schema.Presentation?.Fields.SingleOrDefault(field => field.FieldId == port.Id)
    ?? new FieldPresentation(port.Id, port.Id, "运行参数", 0, FieldEditorKind.Scalar);
   var adapter = new RootResourceProxyBinding(port.Id, port);
   var unavailable = !Supports(port.Type) && (port.EditModes & FieldEditModes.Reference) == 0
    ? "需要专用集合或资源编辑器" : null;
   return new RootParameterCandidate(draft.Node, port.Id, port.Id, presentation.DisplayName,
    new ConfigurationFieldAccessor(port, presentation, port.Id, () => adapter.Literal(draft),
     value => adapter.Write(draft, new ConstantSetting(value))), unavailable, adapter);
  }).ToArray();
 }
 private static bool Supports(TypeRef type) => type.Shape == ValueShape.Scalar ||
  type.Shape == ValueShape.Record && type.Fields.All(field => Supports(field.Type));
 internal static RootParameterCandidate Resolve(NodeConfigurationEditDraft draft, RootParameterProxy proxy)
 {
  if (draft.Node.Id != proxy.NodeId || draft.Node.ToolType != proxy.ToolType)
   throw new InvalidOperationException("代理来源节点或工具已变化。");
  var field = Describe(draft).SingleOrDefault(item => item.FieldId == proxy.FieldId && item.StoragePath == proxy.StoragePath)
   ?? throw new InvalidOperationException("正式字段已移除或存储路径已变化。");
  if (field.Unavailable is not null || field.Field is null) throw new InvalidOperationException(field.Unavailable ?? "字段不可编辑。");
  return field;
 }
}
