using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;

namespace SunEyeVision.Tool.PixelCount.Runtime;

/// <summary>PixelCount 原厂 ROI Area 的工具层字段身份。控件和生命周期由公共 ROI 组装器负责。</summary>
public static class PixelCountRoi
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

    public static TypeRef RectangleType { get; } = TypeRef.RecordOf("pixelcount.roi.rectangle", new[]
    {
        new Port("center-x", DataType.Number), new Port("center-y", DataType.Number),
        new Port("width", DataType.Number), new Port("height", DataType.Number),
        new Port("angle-degrees", DataType.Number)
    });
    public static TypeRef AnnulusType { get; } = TypeRef.RecordOf("pixelcount.roi.annulus", new[]
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
            MaskImageLabel = "输入掩膜",
            OutputMaskFieldId = OutputMask,
            OutputMaskLabel = "输出掩膜",
            MaskSourceFieldId = MaskSource,
            MaskSourceLabel = "屏蔽区",
            MaskInheritedFieldId = MaskInherited,
            AttachAnchorFieldId = PixelCountTool.ImageInput,
            AttachSection = "ROI区域"
        }, attach, observe);
    }
}

/// <summary>PixelCount 的正式 ROI 文档。编辑宿主沿用 Blob 已验证的公共文档与会话链。</summary>
internal static class PixelCountLocalRoi
{
    public const string InputId = PixelCountRoi.LocalDocumentId;
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
        PixelCountRoi.ModeId, PixelCountRoi.Local,
        new(PixelCountRoi.Rectangle, "RectangleParameters", PixelCountRoi.RectangleSource,
            new[] { "center-x", "center-y", "width", "height", "angle-degrees" }, "rectangle"),
        new(PixelCountRoi.Annulus, "AnnulusParameters", PixelCountRoi.AnnulusSource,
            new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" }, "annulus"),
        RoiDocumentValue.MaximumInheritedCount);

    public static MaskInheritanceSpec MaskInheritance { get; } = new(
        PixelCountRoi.MaskInherited, PixelCountRoi.MaskSource, RoiDocumentValue.MaximumInheritedCount);

    public static INativeRoiEditDocument Create(DeclaredInputFieldsView inputs) => new RoiDocumentStore(
        inputs, InputId, Groups, PixelCountTool.ImageInput, Inheritance, MaskInheritance,
        new[] { "检测区域", "屏蔽区域" });
}

/// <summary>PixelCount 原厂 AssistGroup。模式、来源行和显隐由公共 Assist 控件负责。</summary>
public static class PixelCountAssist
{
    public const string ModeId = "input.pose.mode";
    public const string Information = "input.pose.information";
    public const string BasePoint = "input.pose.base-point";
    public const string RunPoint = "input.pose.run-point";

    public static TypeRef PointType { get; } = TypeRef.RecordOf("pixelcount.pose.point", new[]
    {
        new Port("x", DataType.Number), new Port("y", DataType.Number)
    });

    public static IReadOnlyList<Port> Ports { get; } = new[]
    {
        new Port(ModeId, DataType.Integer, Required: false, EditModes: FieldEditModes.Constant),
        new Port(Information, DataType.Text, Required: false, EditModes: FieldEditModes.Reference),
        new Port(BasePoint, PointType, Required: false, Nullable: true, EditModes: FieldEditModes.Reference),
        new Port(RunPoint, PointType, Required: false, Nullable: true, EditModes: FieldEditModes.Reference)
    }
    .Concat(new[] { "x", "y" }.Select(id => new Port(BasePoint + "." + id, DataType.Number,
        Required: false, EditModes: FieldEditModes.Reference)))
    .Concat(new[] { "x", "y" }.Select(id => new Port(RunPoint + "." + id, DataType.Number,
        Required: false, EditModes: FieldEditModes.Reference)))
    .Concat(new[] { "angle", "scale-x", "scale-y" }.SelectMany(id => new[]
    {
        new Port(BasePoint + "." + id, DataType.Number, Required: false, EditModes: FieldEditModes.Reference),
        new Port(RunPoint + "." + id, DataType.Number, Required: false, EditModes: FieldEditModes.Reference)
    }))
    .ToArray();

    public static NativeAssistUiDraftControls Add(DeclaredInputFieldsView inputs,
        Action<FrameworkElement> attach)
    {
        var mode = inputs.ComposeInputEditor(ModeId, "选择方式",
            defaultWhenUnset: new ConstantSetting(0L),
            integerRadioOptions: new NativeIntegerRadioOptions(new[]
            {
                new NativeIntegerRadioChoice(0, "按信息"),
                new NativeIntegerRadioChoice(1, "按点"),
                new NativeIntegerRadioChoice(2, "按坐标")
            }, useFiveSlotTemplate: true));
        var information = inputs.ComposeInputEditor(Information, "修正信息", referenceOnly: true);
        var sources = Sources(inputs).ToArray();
        var assist = new NativeAssistUiDraftControls(mode, information, sources);
        try
        {
            attach(assist);
            return assist;
        }
        catch
        {
            assist.Dispose();
            throw;
        }
    }

    private static IEnumerable<(int Mode, Func<DeclaredFieldRow> Create)> Sources(DeclaredInputFieldsView inputs)
    {
        (int, Func<DeclaredFieldRow>) Source(int mode, string id, string label)
            => (mode, () => inputs.ComposeInputEditor(id, label, referenceOnly: true));

        yield return Source(1, BasePoint, "基准点");
        yield return Source(2, BasePoint + ".x", "基准点X");
        yield return Source(2, BasePoint + ".y", "基准点Y");
        yield return Source(3, BasePoint + ".angle", "基准角度");
        yield return Source(3, BasePoint + ".scale-x", "基准尺度X");
        yield return Source(3, BasePoint + ".scale-y", "基准尺度Y");
        yield return Source(1, RunPoint, "运行点");
        yield return Source(2, RunPoint + ".x", "运行点X");
        yield return Source(2, RunPoint + ".y", "运行点Y");
        yield return Source(3, RunPoint + ".angle", "运行角度");
        yield return Source(3, RunPoint + ".scale-x", "运行尺度X");
        yield return Source(3, RunPoint + ".scale-y", "运行尺度Y");
    }
}
