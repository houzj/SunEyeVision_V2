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
        IToolDefinition tool, string? imageInputFieldId = null, string sampleLabel = "图像源 · 图像")
    {
        ArgumentNullException.ThrowIfNull(tool);
        var draft = new DesignTimeDraft(tool);
        var schema = draft.Schema;
        var inputSection = schema.Presentation!.Fields.First().Layout!.Page;
        if (imageInputFieldId is null)
            return (draft, new(ReferenceOptions: ReferenceFieldOptions.Empty, InputSection: inputSection));

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
            InputSection: inputSection, InputContent: inputs));
    }
}
