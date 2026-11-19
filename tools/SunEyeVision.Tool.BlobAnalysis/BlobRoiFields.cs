using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>
/// Blob 的 ROI 组字段声明：只描述"用哪些字段、什么标签、什么角色"，
/// 不创建任何控件。控件由平台 <see cref="RoiGroupView"/> 的模板创建。
/// <para>
/// 对应原厂 <c>BlobFindRoiManager</c> 只提供字段名列表的做法
/// （<c>roicenterx</c> / <c>roiwidth</c> / <c>roiAnnulusCenterX</c> …）。
/// </para>
/// </summary>
internal static class BlobRoiFields
{
    private static readonly string[] RectangleLabels =
        { "roicenterx", "roicentery", "roiwidth", "roiheight", "roiangle" };

    private static readonly string[] AnnulusLabels =
        { "roiAnnulusCenterX", "roiAnnulusCenterY", "CircleInnerRadius", "roiAnnulusOuterRadius", "Angle Start", "AngleRange" };

    /// <summary>构造本工具的 ROI 组字段清单（顺序即呈现顺序）。</summary>
    internal static IReadOnlyList<RoiFieldRef> Create()
    {
        var fields = new List<RoiFieldRef>
        {
            new(BlobRoiInheritance.ModeId, "ROI类型", RoiFieldRole.DetectionMode),
            new(BlobImageRoi.ModeId, "区域类型", RoiFieldRole.AreaType),
            new(BlobRoiInheritance.RectangleSource, "Region", RoiFieldRole.RectangleSource),
            new(BlobRoiInheritance.AnnulusSource, "Region", RoiFieldRole.AnnulusSource)
        };

        var rectangleMembers = BlobRoiInheritance.RectangleMembers;
        for (var index = 0; index < rectangleMembers.Count && index < RectangleLabels.Length; index++)
            fields.Add(new RoiFieldRef(
                BlobRoiInheritance.Leaf(BlobRoiInheritance.RectangleParameters, rectangleMembers[index]),
                RectangleLabels[index], RoiFieldRole.RectangleParameter));

        var annulusMembers = BlobRoiInheritance.AnnulusMembers;
        for (var index = 0; index < annulusMembers.Count && index < AnnulusLabels.Length; index++)
            fields.Add(new RoiFieldRef(
                BlobRoiInheritance.Leaf(BlobRoiInheritance.AnnulusParameters, annulusMembers[index]),
                AnnulusLabels[index], RoiFieldRole.AnnulusParameter));

        fields.Add(new RoiFieldRef(BlobMaskInheritance.ModeId, "屏蔽区", RoiFieldRole.MaskMode));
        fields.Add(new RoiFieldRef(BlobMaskInheritance.SourceId, "屏蔽区", RoiFieldRole.MaskSource));
        fields.Add(new RoiFieldRef(BlobImageRoi.ImageId, "Mask Image", RoiFieldRole.MaskImage));
        fields.Add(new RoiFieldRef(BlobImageRoi.OutputEnabledId, "OutputMask", RoiFieldRole.OutputMask));
        return fields;
    }

    /// <summary>
    /// 行工厂：由 <see cref="RoiGroupView"/> 的模板调用，创建后归呈现器独占。
    /// 各字段的编辑形态（图像来源下拉 / 整数单选 / 开关）在这里决定。
    /// </summary>
    internal static Func<RoiFieldRef, DeclaredFieldRow> Factory(DeclaredInputFieldsView inputs)
        => field => field.FieldId switch
        {
            BlobImageRoi.ImageId => inputs.ComposeInputEditor(field.FieldId, field.Label, imageSource: true),
            BlobImageRoi.ModeId => inputs.ComposeInputEditor(field.FieldId, field.Label,
                defaultWhenUnset: new ConstantSetting(0L),
                integerRadioOptions: new NativeIntegerRadioOptions(new[]
                {
                    new NativeIntegerRadioChoice(0, AppLocalizationService.GetLang("Type")!),
                    new NativeIntegerRadioChoice(1, AppLocalizationService.GetLang("RunParam_Image Type")!)
                })),
            BlobImageRoi.OutputEnabledId => inputs.ComposeInputEditor(field.FieldId, field.Label,
                defaultWhenUnset: new ConstantSetting(false), showClearButton: false),
            _ => inputs.ComposeInputEditor(field.FieldId, field.Label)
        };
}
