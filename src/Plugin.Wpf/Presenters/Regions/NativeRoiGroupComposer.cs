using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>ROI 组声明：工具只交字段名、模式和标签，行与控件由平台创建。</summary>
public sealed record RoiGroupComposerSpec
{
    /// <summary>检测方式字段（本地绘制 / 继承）。</summary>
    public required string ModeFieldId { get; init; }
    /// <summary>表示本地绘制的取值，其余取值按 <see cref="InheritedModes"/> 顺序解释为继承模式。</summary>
    public required string LocalModeValue { get; init; }
    public required IReadOnlyList<string> InheritedModes { get; init; }
    public required string RectangleSourceFieldId { get; init; }
    public required string AnnulusSourceFieldId { get; init; }
    public required Func<IReadOnlyList<DeclaredFieldRow>> RectangleParameterFields { get; init; }
    public required Func<IReadOnlyList<DeclaredFieldRow>> AnnulusParameterFields { get; init; }
    public required string AreaTypeFieldId { get; init; }
    public required string AreaTypeLabel { get; init; }
    public required IReadOnlyList<NativeIntegerRadioChoice> AreaTypeChoices { get; init; }
    public required string MaskImageFieldId { get; init; }
    public required string MaskImageLabel { get; init; }
    public required string OutputMaskFieldId { get; init; }
    public required string OutputMaskLabel { get; init; }
    public required string MaskSourceFieldId { get; init; }
    public required string MaskSourceLabel { get; init; }
    public required Func<bool> ReadMaskInherited { get; init; }
    public required Action<bool> AcceptMaskInherited { get; init; }
    public required string AttachAnchorFieldId { get; init; }
    public required string AttachSection { get; init; }
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
            inputs.ComposeInputEditor(spec.RectangleSourceFieldId, "Region"),
            inputs.ComposeInputEditor(spec.AnnulusSourceFieldId, "Region"),
            spec.RectangleParameterFields,
            spec.AnnulusParameterFields);

        var maskImage = inputs.ComposeInputEditor(spec.MaskImageFieldId, spec.MaskImageLabel, imageSource: true);
        var outputMask = inputs.ComposeInputEditor(spec.OutputMaskFieldId, spec.OutputMaskLabel,
            defaultWhenUnset: new ConstantSetting(false), showClearButton: false);
        var area = inputs.ComposeInputEditor(spec.AreaTypeFieldId, spec.AreaTypeLabel,
            defaultWhenUnset: new ConstantSetting(0L),
            integerRadioOptions: new NativeIntegerRadioOptions(spec.AreaTypeChoices));
        var maskSource = inputs.ComposeInputEditor(spec.MaskSourceFieldId, spec.MaskSourceLabel);

        var controls = new NativeRoiUiDraftControls(inheritance, maskImage, area, outputMask, maskSource,
            spec.ReadMaskInherited, spec.AcceptMaskInherited);
        try
        {
            attach(spec.AttachAnchorFieldId, controls, spec.AttachSection);
            return controls;
        }
        catch
        {
            controls.Dispose();
            throw;
        }
    }
}
