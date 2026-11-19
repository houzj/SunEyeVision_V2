using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Presenters.Design;

/// <summary>
/// 设计时样本上下文：为参数页构造一份可渲染的假上下文，供设计器与验收探针使用。
/// 不触达发现、文档、算法会话或原生资源；生产运行路径不会调用它。
/// </summary>
public static class DesignTimeSample
{
    /// <summary>构造样本上下文；给出图像输入字段时同时投影一个假图像源。</summary>
    public static (IToolSettingsDraft Draft, ToolSettingsViewOptions Options) Create(
        IToolDefinition tool, string? imageInputFieldId = null, string sampleLabel = "图像源 · 图像",
        ICameraSelectionSource? cameraSource = null)
    {
        ArgumentNullException.ThrowIfNull(tool);
        var draft = new DesignTimeDraft(tool);
        // 输入视图与假引用需要完整契约；草稿内部用的编辑器 schema 不含输入项。
        var schema = tool.Describe();
        var inputSection = draft.Schema.Presentation!.Fields.First().Layout!.Page;
        var cameraSources = cameraSource is null
            ? null
            : new Dictionary<string, ICameraSelectionSource>(StringComparer.Ordinal) { ["camera.device"] = cameraSource };
        // 编辑器 schema 只含可编辑字段；该工具没有此输入项时退回不带假图像源的样本。
        if (imageInputFieldId is null || !schema.Inputs.Any(port => port.Id == imageInputFieldId))
            return (draft, new(ReferenceOptions: ReferenceFieldOptions.Empty, InputSection: inputSection,
                CameraSources: cameraSources));

        var source = new DeclaredReferenceCandidate(sampleLabel, "sample-image", "sample-workflow",
            new Port("output.image", TypeRef.ResourceOf(ResourceKind.Image)), Array.Empty<string>());
        var inputDraft = new FieldSettingsDraft(schema, new FieldSettings(schema.Inputs,
            new Dictionary<string, ParameterSetting>
            {
                [imageInputFieldId] = new ReferenceSetting(source.Reference(BindingSelection.All))
            }));
        var inputs = new DeclaredInputFieldsView(schema,
            port => inputDraft.CaptureFields().Read(port.Id),
            (port, value) => inputDraft.ApplyFields(
                new Dictionary<string, ParameterSetting> { [port.Id] = value }, _ => { }),
            new ReferenceFieldOptions(port => port.Id == imageInputFieldId
                ? new ReferenceCandidates(new[] { source }, Array.Empty<DeclaredReferenceCandidate>())
                : ReferenceCandidates.Empty));
        return (draft, new(ReferenceOptions: ReferenceFieldOptions.Empty,
            InputSection: inputSection, InputContent: inputs, CameraSources: cameraSources));
    }
}
