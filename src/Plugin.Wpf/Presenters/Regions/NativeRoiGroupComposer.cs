using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>ROI 组声明：工具只交字段名、模式和标签，行与控件由平台创建。</summary>
/// <summary>ROI 参数行：字段身份与显示标签，平台据此创建控件行。</summary>
public sealed record RoiParameterRow(string FieldId,string Label);

public sealed record RoiGroupComposerSpec
{
    /// <summary>检测方式字段（本地绘制 / 继承）。</summary>
    public required string ModeFieldId { get; init; }
    /// <summary>表示本地绘制的取值，其余取值按 <see cref="InheritedModes"/> 顺序解释为继承模式。</summary>
    public required string LocalModeValue { get; init; }
    public required IReadOnlyList<string> InheritedModes { get; init; }
    public string? RectangleSourceFieldId { get; init; }
    public string? AnnulusSourceFieldId { get; init; }
    public required IReadOnlyList<RoiParameterRow> RectangleParameterRows { get; init; }
    public required IReadOnlyList<RoiParameterRow> AnnulusParameterRows { get; init; }
    public string? AreaTypeFieldId { get; init; }
    public string? AreaTypeLabel { get; init; }
    public IReadOnlyList<NativeIntegerRadioChoice>? AreaTypeChoices { get; init; }
    public string? MaskImageFieldId { get; init; }
    public string? MaskImageLabel { get; init; }
    public string? OutputMaskFieldId { get; init; }
    public string? OutputMaskLabel { get; init; }
    public string? MaskSourceFieldId { get; init; }
    public string? MaskSourceLabel { get; init; }
    /// <summary>屏蔽区继承开关所在字段；读写由平台按该字段完成。</summary>
    public string? MaskInheritedFieldId { get; init; }
    public required string AttachAnchorFieldId { get; init; }
    public required string AttachSection { get; init; }
    /// <summary>继承模式在原厂页签中显示的选项。缺省保持 Blob/PixelCount 的四种模式。</summary>
    public IReadOnlyList<string>? InheritedModeLabels { get; init; }
    /// <summary>是否呈现 ROI 区域类型、掩膜输入和输出掩膜行。</summary>
    public bool ShowAreaType { get; init; } = true;
    public bool ShowMaskImage { get; init; } = true;
    public bool ShowOutputMask { get; init; } = true;
    /// <summary>仅保留原厂声明的矩形来源时关闭圆形来源行。</summary>
    public bool ShowAnnulusSource { get; init; } = true;
    /// <summary>是否显示原厂屏蔽区创建/屏蔽区行；不是所有工具的原厂基本页都包含它们。</summary>
    public bool ShowMaskGroup { get; init; } = true;
}

/// <summary>按声明装配 ROI 组：字段行、继承绑定、屏蔽区控件和插入位置都由平台决定。</summary>
public static class NativeRoiGroupComposer
{
    public static NativeRoiUiDraftControls Compose(DeclaredInputFieldsView inputs, RoiGroupComposerSpec spec,
        Action<string, FrameworkElement, string?> attach, Action<NativeRoiUiDraftControls>? observe = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(attach);

        Func<IReadOnlyList<DeclaredFieldRow>> Rows(IReadOnlyList<RoiParameterRow> rows)
            => () => rows.Select(row => inputs.ComposeInputEditor(row.FieldId, row.Label)).ToArray();

        DeclaredFieldRow Synthetic(string id, string label, TypeRef type, ParameterSetting value)
            => new(new Port(id, type, Required: false), label, value, _ => { });

        bool ReadMaskInherited() => spec.MaskInheritedFieldId is not { } maskId
            ? false
            : inputs.ReadUnpresentedInput(maskId) switch
            {
                UnsetSetting => false,
                ConstantSetting { Value: bool value } => value,
                _ => throw new ArgumentException("屏蔽区创建方式必须是固定值。")
            };
        void AcceptMaskInherited(bool value)
        {
            if (spec.MaskInheritedFieldId is { } maskId)
                inputs.WriteUnpresentedInput(maskId, new ConstantSetting(value));
        }

        int? ReadMode()
        {
            var value = inputs.ReadUnpresentedInput(spec.ModeFieldId) switch
            {
                UnsetSetting => spec.LocalModeValue,
                ConstantSetting { Value: string text } => text,
                _ => throw new ArgumentException("ROI 继承方式无效。")
            };
            if (string.Equals(value, spec.LocalModeValue, StringComparison.Ordinal)) return null;
            var index = -1;
            for (var position = 0; position < spec.InheritedModes.Count; position++)
                if (string.Equals(spec.InheritedModes[position], value, StringComparison.Ordinal)) { index = position; break; }
            return index < 0 ? throw new ArgumentException("未知的 ROI 继承方式: " + value) : index;
        }

        var inheritance = new NativeRoiInheritanceBinding(ReadMode, selected =>
            inputs.WriteUnpresentedInput(spec.ModeFieldId, new ConstantSetting(selected is null
                ? spec.LocalModeValue
                : spec.InheritedModes[selected.Value])),
            spec.RectangleSourceFieldId is { } rectangleId
                ? inputs.ComposeInputEditor(rectangleId, "Region")
                : Synthetic("roi.rectangle.hidden", "Region", TypeRef.ResourceOf(ResourceKind.Region), new UnsetSetting()),
            spec.AnnulusSourceFieldId is { } annulusId
                ? inputs.ComposeInputEditor(annulusId, "Region")
                : Synthetic("roi.annulus.hidden", "Region", TypeRef.ResourceOf(ResourceKind.Region), new UnsetSetting()),
            Rows(spec.RectangleParameterRows),
            Rows(spec.AnnulusParameterRows));

        var maskImage = spec.MaskImageFieldId is { } maskImageId
            ? inputs.ComposeInputEditor(maskImageId, spec.MaskImageLabel ?? "输入掩膜", imageSource: true)
            : null;
        var outputMask = spec.OutputMaskFieldId is { } outputMaskId
            ? inputs.ComposeInputEditor(outputMaskId, spec.OutputMaskLabel ?? "输出掩膜",
                defaultWhenUnset: new ConstantSetting(false), showClearButton: false)
            : null;
        var area = spec.AreaTypeFieldId is { } areaId
            ? inputs.ComposeInputEditor(areaId, spec.AreaTypeLabel ?? "ROI区域类型",
                defaultWhenUnset: new ConstantSetting(0L),
                integerRadioOptions: new NativeIntegerRadioOptions(spec.AreaTypeChoices ?? Array.Empty<NativeIntegerRadioChoice>()))
            : null;
        var maskSource = spec.MaskSourceFieldId is { } maskSourceId
            ? inputs.ComposeInputEditor(maskSourceId, spec.MaskSourceLabel ?? "屏蔽区")
            : null;

        var controls = new NativeRoiUiDraftControls(inheritance, maskImage, area, outputMask, maskSource,
            ReadMaskInherited, AcceptMaskInherited, spec.ShowMaskGroup, spec.ShowAreaType,
            spec.ShowMaskImage, spec.ShowOutputMask, spec.ShowAnnulusSource, spec.InheritedModeLabels);
        try
        {
            attach(spec.AttachAnchorFieldId, controls, spec.AttachSection);
            // 回传装配结果：调用方（工具的页面装配器）需要这份引用挂接编辑宿主。
            observe?.Invoke(controls);
            return controls;
        }
        catch
        {
            controls.Dispose();
            throw;
        }
    }
}
