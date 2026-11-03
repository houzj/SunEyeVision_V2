using SunEyeVision.Application.Editing;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Adapts an admitted field on a private node draft without retaining invocation resources.</summary>
internal sealed record RootResourceProxyBinding(string StoragePath, Port Port)
{
 internal ConfigurationFieldAccessor Field(string display, NodeConfigurationEditDraft draft) =>
  new(Port, new(Port.Id, display, "输入订阅", 0, FieldEditorKind.Scalar), StoragePath,
   () => Literal(draft), value => Write(draft, new ConstantSetting(value)));
 internal ParameterSetting Read(NodeConfigurationEditDraft draft) => draft.Configuration.Fields.Read(StoragePath);
 internal void Write(NodeConfigurationEditDraft draft, ParameterSetting setting) =>
  draft.ApplyFields(new Dictionary<string, ParameterSetting> { [StoragePath] = setting });
 internal object? Literal(NodeConfigurationEditDraft draft) =>
  Read(draft) is ConstantSetting constant ? constant.Value : null;
}
