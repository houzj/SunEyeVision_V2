using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.ContourMatch.Runtime;
using SunEyeVision.Tool.GrayMatch.Runtime;

namespace SunEyeVision.Tools.Inspection.Tests.Fixtures;

/// <summary>
/// 参数页/结果页契约测试的编辑器夹具。
///
/// 这些逻辑此前放在生产代码的 <c>*DesignData.cs</c> 里（Visual Studio 设计器用），
/// 导致生产代码承担测试职责——改字段要同步改夹具、删代码要担心测试。
/// 夹具归测试项目后，生产代码只剩运行期路径。
///
/// 构造方式与运行期一致：真实 Tool + 真实声明链，不创建文档、执行会话或原生资源。
/// </summary>
internal static class EditorFixture
{
    /// <summary>轮廓匹配：输入区锚定图像源分组，特征模板页由整页内容独占。</summary>
    internal static (IToolSettingsDraft Draft, ToolSettingsViewOptions Options) ContourMatch()
    {
        var tool = new ContourMatchTool();
        var draft = new ContourMatchDraft(tool);
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
            InputSection: ContourMatchFields.ImageInputSection, InputContent: inputs));
    }

    /// <summary>灰度匹配：输入区锚定模板分组。</summary>
    internal static (IToolSettingsDraft Draft, ToolSettingsViewOptions Options) GrayMatch()
    {
        var tool = new GrayMatchTool();
        var draft = new GrayMatchDraft(tool);
        var schema = tool.Describe();
        var image = new DeclaredReferenceCandidate("图像源 · 图像", "sample-image", "sample-workflow",
            new Port("output.image", TypeRef.ResourceOf(ResourceKind.Image)), Array.Empty<string>());
        var region = new DeclaredReferenceCandidate("上游工具 · 区域", "sample-region", "sample-workflow",
            new Port("output.region", TypeRef.ResourceOf(ResourceKind.Region)), Array.Empty<string>());
        var inputDraft = new FieldSettingsDraft(schema, new FieldSettings(schema.Inputs, new Dictionary<string, ParameterSetting>
        {
            [GrayMatchSchema.ImageInput] = new ReferenceSetting(image.Reference(BindingSelection.All)),
            [GrayMatchSchema.RoiInput] = new ReferenceSetting(region.Reference(BindingSelection.All))
        }));
        var inputs = new DeclaredInputFieldsView(schema, port => inputDraft.CaptureFields().Read(port.Id),
            (port, value) => inputDraft.ApplyFields(new Dictionary<string, ParameterSetting> { [port.Id] = value }, _ => { }),
            new ReferenceFieldOptions(port => port.Id switch
            {
                GrayMatchSchema.ImageInput => new ReferenceCandidates(new[] { image }, Array.Empty<DeclaredReferenceCandidate>()),
                GrayMatchSchema.RoiInput => new ReferenceCandidates(new[] { region }, Array.Empty<DeclaredReferenceCandidate>()),
                _ => ReferenceCandidates.Empty
            }));
        return (draft, new ToolSettingsViewOptions(ReferenceOptions: ReferenceFieldOptions.Empty,
            InputSection: GrayMatchFields.TemplateSection, InputContent: inputs));
    }

    private sealed class ContourMatchDraft : IToolSettingsDraft
    {
        private readonly FieldSettingsDraft _draft;
        private readonly ContourMatchTool _tool;

        internal ContourMatchDraft(ContourMatchTool tool)
        {
            _tool = tool;
            var schema = ResultDisplayFields.EditorSchema(tool);
            var values = ((IToolSettingsFields)tool).ReadSettings(new ContourMatchSettings()).CopyValues()
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
            var settings = ((IToolSettingsFields)_tool).MaterializeSettings(
                fields.Select(_tool.SettingsSchema.Inputs).Materialize(new Dictionary<string, object?>()));
            var errors = ((IToolDefinition)_tool).Validate(settings);
            if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
            return fields;
        }

        public void ApplyFields(IReadOnlyDictionary<string, ParameterSetting> changes, Action<FieldSettings> acceptCandidate)
            => _draft.ApplyFields(changes, acceptCandidate);
    }

    private sealed class GrayMatchDraft : IToolSettingsDraft
    {
        private readonly FieldSettingsDraft _draft;
        private readonly GrayMatchTool _tool;

        internal GrayMatchDraft(GrayMatchTool tool)
        {
            _tool = tool;
            var schema = ResultDisplayFields.EditorSchema(tool);
            var values = ((IToolSettingsFields)tool).ReadSettings(new GrayMatchSettings()).CopyValues()
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
            var settings = ((IToolSettingsFields)_tool).MaterializeSettings(
                fields.Select(_tool.SettingsSchema.Inputs).Materialize(new Dictionary<string, object?>()));
            var errors = ((IToolDefinition)_tool).Validate(settings);
            if (errors.Count != 0) throw new ArgumentException(string.Join("；", errors.Select(error => error.Message)));
            return fields;
        }

        public void ApplyFields(IReadOnlyDictionary<string, ParameterSetting> changes, Action<FieldSettings> acceptCandidate)
            => _draft.ApplyFields(changes, acceptCandidate);
    }
}
