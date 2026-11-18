using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.CircleFind.Runtime;

namespace SunEyeVision.Tool.CircleFind.Wpf.Design;

/// <summary>
/// 设计时示例数据：只用于 Visual Studio 设计器，走与运行期相同的声明与组装链，
/// 不创建文档、执行会话或原生资源。
/// </summary>
public static class CircleFindDesignData
{
    public const string InputPage = "运行参数";

    public static (IToolSettingsDraft Draft, ToolSettingsViewOptions Options) CreateParameterContext()
    {
        var tool = new CircleFindTool();
        var draft = new DesignDraft(tool);
        var schema = tool.Describe();
        var image = new DeclaredReferenceCandidate("图像源 · 图像", "sample-image", "sample-workflow",
            new Port("output.image", TypeRef.ResourceOf(ResourceKind.Image)), Array.Empty<string>());
        var region = new DeclaredReferenceCandidate("上游工具 · 区域", "sample-region", "sample-workflow",
            new Port("output.region", TypeRef.ResourceOf(ResourceKind.Region)), Array.Empty<string>());
        var inputDraft = new FieldSettingsDraft(schema, new FieldSettings(schema.Inputs, new Dictionary<string, ParameterSetting>
        {
            [CircleFindSchema.ImageInput] = new ReferenceSetting(image.Reference(BindingSelection.All)),
            [CircleFindSchema.RoiInput] = new ReferenceSetting(region.Reference(BindingSelection.All))
        }));
        var inputs = new DeclaredInputFieldsView(schema, port => inputDraft.CaptureFields().Read(port.Id),
            (port, value) => inputDraft.ApplyFields(new Dictionary<string, ParameterSetting> { [port.Id] = value }, _ => { }),
            new ReferenceFieldOptions(port => port.Id switch
            {
                CircleFindSchema.ImageInput => new ReferenceCandidates(new[] { image }, Array.Empty<DeclaredReferenceCandidate>()),
                CircleFindSchema.RoiInput => new ReferenceCandidates(new[] { region }, Array.Empty<DeclaredReferenceCandidate>()),
                _ => ReferenceCandidates.Empty
            }));
        return (draft, new ToolSettingsViewOptions(ReferenceOptions: ReferenceFieldOptions.Empty,
            InputSection: InputPage, InputContent: inputs));
    }

    private sealed class DesignDraft : IToolSettingsDraft
    {
        private readonly FieldSettingsDraft _draft;
        internal CircleFindTool Tool { get; }

        internal DesignDraft(CircleFindTool tool)
        {
            Tool = tool;
            var schema = ResultDisplayFields.EditorSchema(tool);
            var settings = new CircleFindSettings();
            var values = ((IToolSettingsFields)tool).ReadSettings(settings).CopyValues()
                .Concat(tool.CreateDisplaySettings().CopyValues())
                .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);
            _draft = new(schema, new FieldSettings(schema.Inputs, values));
        }

        public ToolSchema Schema => _draft.Schema;
        public long Revision => _draft.Revision;
        public FieldSettings CaptureFields() => _draft.CaptureFields();

        public FieldSettings CaptureValidatedFields() => _draft.CaptureValidatedFields();

        public void ApplyFields(IReadOnlyDictionary<string, ParameterSetting> changes, Action<FieldSettings> acceptCandidate)
            => _draft.ApplyFields(changes, acceptCandidate);
    }
}
