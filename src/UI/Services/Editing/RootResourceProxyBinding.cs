using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Workflow;
using Setting = SunEyeVision.Plugin.Contracts.ParameterSetting;
namespace SunEyeVision.UI.Services.Editing;
/// <summary>Runtime field adapter on a private node draft; it never retains borrowed resource values.</summary>
internal sealed record RootResourceProxyBinding(string StoragePath, Port Port, bool ImageSource, bool Runtime = false)
{
 internal ConfigurationFieldAccessor Field(string display) => new(Port, new(Port.Id, display, "输入订阅", 0, FieldEditorKind.Scalar), StoragePath,
  () => null, _ => throw new InvalidOperationException("运行资源只能订阅来源，不能写常量。"));
 internal Setting Read(WorkflowNodeBase node, DataSourceQueryService query, string rootId) => node.RuntimeConfiguration!.Fields.Read(StoragePath);
 internal void Write(WorkflowNodeBase node, Setting setting, DataSourceQueryService query, string rootId)
 {
  if (setting is ReferenceSetting reference)
  {
   if (reference.Reference.ScopeId != rootId) throw new InvalidOperationException("来源跨出当前根流程。");
   var source = query.GetSchema(reference.Reference.NodeId)?.Outputs.SingleOrDefault(port => port.Id == reference.Reference.PortId)
    ?? throw new InvalidOperationException("正式资源来源已失效，请解除或重新选择。");
   if (source.Type != reference.Reference.Type || source.Storage != reference.Reference.Storage)
    throw new InvalidOperationException("来源正式声明已变化，请解除或重新选择。");
  }
  node.RuntimeConfiguration = node.RuntimeConfiguration!.WithFieldSettings(new Dictionary<string, Setting> { [StoragePath] = setting });
 }
 internal void RestoreOpening(WorkflowNodeBase node, WorkflowNodeBase opening)
  => node.RuntimeConfiguration = node.RuntimeConfiguration!.RestoreFieldFrom(StoragePath, opening.RuntimeConfiguration!);
 internal bool IsSettings(WorkflowNodeBase node) => node.RuntimeConfiguration!.SettingsSchema?.Inputs.Any(field => field.Id == StoragePath) == true;
 internal object? Literal(WorkflowNodeBase node) => node.RuntimeConfiguration!.Fields.Read(StoragePath) is ConstantSetting constant ? constant.Value : null;
}
