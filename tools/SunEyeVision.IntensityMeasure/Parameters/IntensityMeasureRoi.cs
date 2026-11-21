using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;

namespace SunEyeVision.Tool.IntensityMeasure.Runtime;

/// <summary>
/// 亮度测量原厂 ROI Area 的工具层字段身份（ROISelecter、掩膜、继承）。
/// 控件与生命周期由公共 ROI 组装器负责，工具只声明字段、模式和标签。
/// 证据：IMVSIntensityMeasureModuAlgorithmTab.xml 的 Tab_ROI Area 分类。
/// </summary>
public static class IntensityMeasureRoi
{
    public const string LocalDocumentId = "input.roi.local";
    public const string ModeId = "input.roi.inheritance-mode";
    public const string Local = "Local";
    public const string Rectangle = "Rectangle";
    public const string Annulus = "Annulus";
    public const string RectangleSource = "input.roi.rectangle";
    public const string AnnulusSource = "input.roi.annulus";
    public const string AreaType = "input.roi.area-type";
    public const string MaskImage = "input.roi.mask-image";
    public const string OutputMask = "input.roi.output-mask";
    public const string MaskSource = "input.roi.mask-source";
    public const string MaskInherited = "input.roi.mask-inherited";

    public static TypeRef RectangleType { get; } = TypeRef.RecordOf("intensitymeasure.roi.rectangle", new[]
    {
        new Port("center-x", DataType.Number), new Port("center-y", DataType.Number),
        new Port("width", DataType.Number), new Port("height", DataType.Number),
        new Port("angle-degrees", DataType.Number)
    });
    public static TypeRef AnnulusType { get; } = TypeRef.RecordOf("intensitymeasure.roi.annulus", new[]
    {
        new Port("center-x", DataType.Number), new Port("center-y", DataType.Number),
        new Port("inner-radius", DataType.Number), new Port("outer-radius", DataType.Number),
        new Port("start-degrees", DataType.Number), new Port("sweep-degrees", DataType.Number)
    });
    public static TypeRef PolygonStrings { get; } = TypeRef.SequenceOf(DataType.Text);

    public static IReadOnlyList<Port> Ports { get; } = new[]
    {
        new Port(ModeId, DataType.Text, Required: false, EditModes: FieldEditModes.Constant),
        new Port(RectangleSource, RectangleType, Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        new Port(AnnulusSource, AnnulusType, Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        new Port(AreaType, DataType.Integer, Required: false, EditModes: FieldEditModes.Constant),
        new Port(MaskImage, TypeRef.ResourceOf(ResourceKind.Image), Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        new Port(OutputMask, DataType.Boolean, Required: false, EditModes: FieldEditModes.Constant),
        new Port(MaskSource, PolygonStrings, Required: false, EditModes: FieldEditModes.Reference),
        new Port(MaskInherited, DataType.Boolean, Required: false, EditModes: FieldEditModes.Constant)
    }.Concat(new[] { "center-x", "center-y", "width", "height", "angle-degrees" }
        .Select(id => new Port(RectangleSource + "." + id, DataType.Number, Required: false, EditModes: FieldEditModes.Reference)))
     .Concat(new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" }
        .Select(id => new Port(AnnulusSource + "." + id, DataType.Number, Required: false, EditModes: FieldEditModes.Reference)))
     .ToArray();

    public static NativeRoiUiDraftControls Add(DeclaredInputFieldsView inputs,
        Action<string, FrameworkElement, string?> attach,
        Action<NativeRoiUiDraftControls>? observe = null)
    {
        var rectangleRows = new[] { "center-x", "center-y", "width", "height", "angle-degrees" }
            .Select((member, index) => new RoiParameterRow(
                RectangleSource + "." + member,
                new[] { "ROI中心X", "ROI中心Y", "ROI宽度", "ROI高度", "ROI角度" }[index])).ToArray();
        var annulusRows = new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" }
            .Select((member, index) => new RoiParameterRow(
                AnnulusSource + "." + member,
                new[] { "圆中心X", "圆中心Y", "内径", "外径", "起始角", "角度范围" }[index])).ToArray();
        return NativeRoiGroupComposer.Compose(inputs, new RoiGroupComposerSpec
        {
            ModeFieldId = ModeId,
            LocalModeValue = Local,
            InheritedModes = new[] { Rectangle, Annulus },
            RectangleSourceFieldId = RectangleSource,
            AnnulusSourceFieldId = AnnulusSource,
            RectangleParameterRows = rectangleRows,
            AnnulusParameterRows = annulusRows,
            AreaTypeFieldId = AreaType,
            AreaTypeLabel = "ROI区域类型",
            AreaTypeChoices = new[] { new NativeIntegerRadioChoice(0, "图形类型"), new NativeIntegerRadioChoice(1, "图像类型") },
            MaskImageFieldId = MaskImage,
            MaskImageLabel = "输入掩膜图",
            OutputMaskFieldId = OutputMask,
            OutputMaskLabel = "输出掩膜",
            MaskSourceFieldId = MaskSource,
            MaskSourceLabel = "屏蔽区",
            MaskInheritedFieldId = MaskInherited,
            ShowMaskGroup = false,
            AttachAnchorFieldId = IntensityMeasureTool.ImageInput,
            AttachSection = "ROI区域"
        }, attach, observe);
    }
}

/// <summary>亮度测量的本地区域文档，沿用 PixelCount/Blob 已验证的公共文档与会话链。</summary>
internal static class IntensityMeasureLocalRoi
{
    public const string InputId = IntensityMeasureRoi.LocalDocumentId;
    public const int MaximumInspectionCount = 16;
    public const int MaximumMaskCount = 8;

    public static IReadOnlyList<NativeRoiEditGroup> Groups { get; } = Array.AsReadOnly(new[]
    {
        new NativeRoiEditGroup(RoiDocumentValue.Inspection, "形状", false,
            MaximumInspectionCount, RoiDocumentValue.FullDomain),
        new NativeRoiEditGroup(RoiDocumentValue.Mask, "屏蔽区", true,
            MaximumMaskCount, RoiDocumentValue.None)
    });

    public static RoiInheritanceSpec Inheritance { get; } = new(
        IntensityMeasureRoi.ModeId, IntensityMeasureRoi.Local,
        new(IntensityMeasureRoi.Rectangle, "RectangleParameters", IntensityMeasureRoi.RectangleSource,
            new[] { "center-x", "center-y", "width", "height", "angle-degrees" }, "rectangle"),
        new(IntensityMeasureRoi.Annulus, "AnnulusParameters", IntensityMeasureRoi.AnnulusSource,
            new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" }, "annulus"),
        RoiDocumentValue.MaximumInheritedCount);

    public static MaskInheritanceSpec MaskInheritance { get; } = new(
        IntensityMeasureRoi.MaskInherited, IntensityMeasureRoi.MaskSource, RoiDocumentValue.MaximumInheritedCount);

    public static INativeRoiEditDocument Create(DeclaredInputFieldsView inputs) => new RoiDocumentStore(
        inputs, InputId, Groups, IntensityMeasureTool.ImageInput, Inheritance, MaskInheritance,
        new[] { "检测区域", "屏蔽区域" });
}
