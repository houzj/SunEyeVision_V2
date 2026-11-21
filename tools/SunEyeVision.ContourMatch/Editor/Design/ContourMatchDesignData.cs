using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Design;

/// <summary>
/// 设计时示例数据：只用于 Visual Studio 设计器，走与运行期相同的声明与组装链，
/// 不创建文档、执行会话或原生资源。
/// </summary>
public static class ContourMatchDesignData
{
    /// <summary>输入区锚定的正式分组：图像源在基本参数页（ModelData 字段所在组），特征模板页由整页内容独占。</summary>
    public const string InputPage = ContourMatchFields.ImageInputSection;

    public static (IToolSettingsDraft Draft, ToolSettingsViewOptions Options) CreateParameterContext()
    {
        var tool = new ContourMatchTool();
        var draft = new DesignDraft(tool);
        var schema = tool.Describe();
        var image = new DeclaredReferenceCandidate("图像源 · 图像", "sample-image", "sample-workflow",
            new Port("output.image", TypeRef.ResourceOf(ResourceKind.Image)), Array.Empty<string>());
        var region = new DeclaredReferenceCandidate("上游工具 · 区域", "sample-region", "sample-workflow",
            new Port("output.region", TypeRef.ResourceOf(ResourceKind.Region)), Array.Empty<string>());
        var inputDraft = new FieldSettingsDraft(schema, new FieldSettings(schema.Inputs, new Dictionary<string, ParameterSetting>
        {
            [ContourMatchSchema.ImageInput] = new ReferenceSetting(image.Reference(BindingSelection.All)),
            [ContourMatchSchema.RoiInput] = new ReferenceSetting(region.Reference(BindingSelection.All))
        }));
        var inputs = new DeclaredInputFieldsView(schema, port => inputDraft.CaptureFields().Read(port.Id),
            (port, value) => inputDraft.ApplyFields(new Dictionary<string, ParameterSetting> { [port.Id] = value }, _ => { }),
            new ReferenceFieldOptions(port => port.Id switch
            {
                ContourMatchSchema.ImageInput => new ReferenceCandidates(new[] { image }, Array.Empty<DeclaredReferenceCandidate>()),
                ContourMatchSchema.RoiInput => new ReferenceCandidates(new[] { region }, Array.Empty<DeclaredReferenceCandidate>()),
                _ => ReferenceCandidates.Empty
            }));
        return (draft, new ToolSettingsViewOptions(ReferenceOptions: ReferenceFieldOptions.Empty,
            InputSection: InputPage, InputContent: inputs));
    }

    private sealed class DesignDraft : IToolSettingsDraft
    {
        private readonly FieldSettingsDraft _draft;
        internal ContourMatchTool Tool { get; }

        internal DesignDraft(ContourMatchTool tool)
        {
            Tool = tool;
            var schema = ResultDisplayFields.EditorSchema(tool);
            var settings = new ContourMatchSettings();
            var values = ((IToolSettingsFields)tool).ReadSettings(settings).CopyValues()
                .Concat(tool.CreateDisplaySettings().CopyValues())
                .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);
            _draft = new(schema, new FieldSettings(schema.Inputs, values));
        }

        public ToolSchema Schema => _draft.Schema;
        public long Revision => _draft.Revision;
        public FieldSettings CaptureFields() => _draft.CaptureFields();

        public FieldSettings CaptureValidatedFields()
        {
            var fields = _draft.CaptureValidatedFields();
            if (fields.Capture().Values.Any(value => value is ReferenceSetting)) return fields;
            var settings = ((IToolSettingsFields)Tool).MaterializeSettings(
                fields.Select(Tool.SettingsSchema.Inputs).Materialize(new Dictionary<string, object?>()));
            var errors = ((IToolDefinition)Tool).Validate(settings);
            if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
            return fields;
        }

        public void ApplyFields(IReadOnlyDictionary<string, ParameterSetting> changes, Action<FieldSettings> acceptCandidate)
            => _draft.ApplyFields(changes, acceptCandidate);
    }
}
