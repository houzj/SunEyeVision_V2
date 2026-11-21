using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Tool.ContourMatch.Runtime;
using static SunEyeVision.Tool.ContourMatch.Runtime.ContourMatchRoi;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 轮廓匹配基本页 ROI 区域装配，按原厂 Tab_ROI Area 的 GroupLinkItem 结构（输出/输入掩膜/RoiSelect/继承/屏蔽/区域类型）
/// 复用平台 NativeRoiGroupComposer。字段声明在 Runtime/ContourMatchRoi.cs（保持 Runtime 不依赖 UI）。
/// </summary>
internal static class ContourMatchRoiComposer
{
    public static NativeRoiUiDraftControls Add(DeclaredInputFieldsView inputs,
        Action<string, FrameworkElement, string?> attach,
        Action<NativeRoiUiDraftControls>? observe = null)
    {
        // 原厂标签（vm-contourmatch-ui-20261121：ROI创建=继承 按区域/按参数 两态截图）：
        // 继承方式只有两项「按区域/按参数」；按参数显示 ROI中心点X/Y/宽度/高度/角度 5 行。
        var rectangleRows = new[] { "center-x", "center-y", "width", "height", "angle-degrees" }
            .Select((member, index) => new RoiParameterRow(
                RectangleSource + "." + member,
                new[] { "ROI中心点X", "ROI中心点Y", "ROI宽度", "ROI高度", "ROI角度" }[index])).ToArray();
        var annulusRows = Array.Empty<RoiParameterRow>();
        return NativeRoiGroupComposer.Compose(inputs, new RoiGroupComposerSpec
        {
            ModeFieldId = ModeId,
            LocalModeValue = Local,
            // 用 InheritedModeLabels 把平台默认四项 radio 收敛为原厂两项，隐藏圆形两项。
            InheritedModeLabels = new[] { "按区域", "按参数" },
            InheritedModes = new[] { Rectangle, RectangleParameters },
            RectangleSourceFieldId = RectangleSource,
            AnnulusSourceFieldId = AnnulusSource, // 保留声明字段避免 Synthetic 无呈现报错；由 ShowAnnulusSource=false 隐藏
            RectangleParameterRows = rectangleRows,
            AnnulusParameterRows = annulusRows,
            AreaTypeFieldId = AreaType,
            AreaTypeLabel = "ROI区域类型",
            AreaTypeChoices = new[] { new NativeIntegerRadioChoice(0, "图形类型"), new NativeIntegerRadioChoice(1, "图像类型") },
            MaskImageFieldId = MaskImage,
            MaskImageLabel = "输入掩膜",
            OutputMaskFieldId = OutputMask,
            OutputMaskLabel = "输出掩膜",
            MaskSourceFieldId = MaskSource,
            MaskSourceLabel = "屏蔽区",
            MaskInheritedFieldId = MaskInherited,
            ShowAnnulusSource = false, // 原厂轮廓匹配无圆形继承
            AttachAnchorFieldId = ContourMatchSchema.ImageInput,
            AttachSection = "ROI区域"
        }, attach, observe);
    }

    public static INativeRoiEditDocument CreateDocument(DeclaredInputFieldsView inputs) => new RoiDocumentStore(
        inputs, LocalDocumentId, Groups, ContourMatchSchema.ImageInput, Inheritance, MaskInheritance,
        new[] { "检测区域", "屏蔽区域" });

    private static IReadOnlyList<NativeRoiEditGroup> Groups { get; } = Array.AsReadOnly(new[]
    {
        new NativeRoiEditGroup(RoiDocumentValue.Inspection, "形状", false, 16, RoiDocumentValue.FullDomain),
        new NativeRoiEditGroup(RoiDocumentValue.Mask, "屏蔽区", true, 8, RoiDocumentValue.None)
    });

    private static RoiInheritanceSpec Inheritance { get; } = new(
        ModeId, Local,
        new(Rectangle, "RectangleParameters", RectangleSource,
            new[] { "center-x", "center-y", "width", "height", "angle-degrees" }, "rectangle"),
        new(Annulus, "AnnulusParameters", AnnulusSource,
            new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" }, "annulus"),
        MaximumInherited);

    private static MaskInheritanceSpec MaskInheritance { get; } = new(MaskInherited, MaskSource, MaximumInherited);
}
