using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;

namespace SunEyeVision.Tool.BlobLabels.Runtime;

/// <summary>
/// Blob 标签分析基本页的 ROI 声明。原厂 AlgorithmTab 把图像来源、ROI
/// 类型/创建、屏蔽区和位置修正放在同一条组装链，工具层只提供字段身份。
/// </summary>
public static class BlobLabelsRoi
{
    public const string LocalDocumentId = "input.roi.local";
    public const string ModeId = "input.roi.inheritance-mode";

    public static RoiInheritanceSpec Inheritance { get; } = new(
        ModeId, Local,
        new(Rectangle, "RectangleParameters", RectangleSource,
            new[] { "center-x", "center-y", "width", "height", "angle-degrees" }, "rectangle"),
        new(Annulus, "AnnulusParameters", AnnulusSource,
            new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" }, "annulus"),
        RoiDocumentValue.MaximumInheritedCount);

    /// <summary>结果视图与参数页共用的 ROI 编辑声明（原件 ROISelecter 常驻语义）。</summary>
    public static RoiEditDeclaration RoiEdit { get; } = new(
        LocalDocumentId, BlobLabelsSchema.ImageInput,
        new[]
        {
            new RoiEditGroupDeclaration(RoiDocumentValue.Inspection, "形状", false,
                BlobLabelsLocalRoi.MaximumInspectionCount, RoiDocumentValue.FullDomain),
            new RoiEditGroupDeclaration(RoiDocumentValue.Mask, "屏蔽区", true,
                BlobLabelsLocalRoi.MaximumMaskCount, RoiDocumentValue.None)
        },
        Inheritance: Inheritance,
        MaskInheritance: new RoiMaskInheritanceDeclaration(MaskInherited, BlobLabelsSchema.MaskRegionInput,
            RoiDocumentValue.MaximumInheritedCount),
        ItemNames: new[] { "检测区域", "屏蔽区域" });
    public const string Local = "Local";
    public const string Rectangle = "Rectangle";
    public const string RectangleParameters = "RectangleParameters";
    public const string Annulus = "Annulus";
    public const string AnnulusParameters = "AnnulusParameters";
    public static string RectangleSource => BlobLabelsSchema.InspectionRoiInput;
    public static string AnnulusSource => BlobLabelsSchema.AnnulusRoiInput;
    public const string AreaType = "input.roi.area-type";
    public const string OutputMask = "input.roi.output-mask";
    public const string MaskInherited = "input.roi.mask-inherited";

    public static TypeRef RectangleType { get; } = TypeRef.RecordOf("bloblabels.roi.rectangle", new[]
    {
        new Port("center-x", DataType.Number), new Port("center-y", DataType.Number),
        new Port("width", DataType.Number), new Port("height", DataType.Number),
        new Port("angle-degrees", DataType.Number)
    });

    public static TypeRef AnnulusType { get; } = TypeRef.RecordOf("bloblabels.roi.annulus", new[]
    {
        new Port("center-x", DataType.Number), new Port("center-y", DataType.Number),
        new Port("inner-radius", DataType.Number), new Port("outer-radius", DataType.Number),
        new Port("start-degrees", DataType.Number), new Port("sweep-degrees", DataType.Number)
    });

    public static TypeRef PolygonStrings { get; } = TypeRef.SequenceOf(DataType.Text);

    // input.image/input.mask/input.roi/input.annulus-roi/input.mask-region 已由
    // BlobLabelsSchema 声明，这里只追加复合组拥有的字段和叶字段。
    public static IReadOnlyList<Port> Ports { get; } = new[]
    {
        new Port(LocalDocumentId, RoiDocumentValue.Declaration, Required: false, EditModes: FieldEditModes.Constant),
        new Port(ModeId, DataType.Text, Required: false, EditModes: FieldEditModes.Constant),
        new Port(AreaType, DataType.Integer, Required: false, EditModes: FieldEditModes.Constant),
        new Port(OutputMask, DataType.Boolean, Required: false, EditModes: FieldEditModes.Constant),
        new Port(MaskInherited, DataType.Boolean, Required: false, EditModes: FieldEditModes.Constant)
    }
    .Concat(new[] { "center-x", "center-y", "width", "height", "angle-degrees" }
        .Select(id => new Port(RectangleSource + "." + id, DataType.Number,
            Required: false, EditModes: FieldEditModes.Reference)))
    .Concat(new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" }
        .Select(id => new Port(AnnulusSource + "." + id, DataType.Number,
            Required: false, EditModes: FieldEditModes.Reference)))
    .ToArray();

    public static RecordValue Materialize(InputValues inputs, RecordValue? local)
    {
        var mode = inputs.Contains(ModeId) ? inputs.Get<string>(ModeId) : Local;
        if (mode == Local) return local ?? RoiDocumentValue.Empty;
        RecordValue shape;
        double Number(string id)
        {
            var value = inputs.Get<double>(id);
            if (!double.IsFinite(value)) throw new ArgumentException("ROI继承参数必须有限。", id);
            return value;
        }
        if (mode == RectangleParameters)
        {
            shape = new RecordValue(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
            {
                ["kind"] = "rectangle",
                ["center"] = RoiDocumentValue.Point(Number(RectangleSource + ".center-x"), Number(RectangleSource + ".center-y")),
                ["width"] = Number(RectangleSource + ".width"),
                ["height"] = Number(RectangleSource + ".height"),
                ["angle-degrees"] = Number(RectangleSource + ".angle-degrees")
            });
        }
        else if (mode == AnnulusParameters)
        {
            var start = RoiDocumentValue.NormalizeDegrees(Number(AnnulusSource + ".start-degrees")) * Math.PI / 180d;
            var sweep = Number(AnnulusSource + ".sweep-degrees");
            if (Math.Abs(sweep) > 360) throw new ArgumentException("圆环继承角度范围不能超过 360 度。", AnnulusSource);
            shape = new RecordValue(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
            {
                ["kind"] = "annulus",
                ["center"] = RoiDocumentValue.Point(Number(AnnulusSource + ".center-x"), Number(AnnulusSource + ".center-y")),
                ["inner-radius"] = Number(AnnulusSource + ".inner-radius"),
                ["outer-radius"] = Number(AnnulusSource + ".outer-radius"),
                ["start-radians"] = start,
                ["end-radians"] = start + sweep * Math.PI / 180d
            });
        }
        else
        {
            throw new ArgumentException("当前 BlobLabels 继承模式需要区域输入，不能物化为结果 ROI。", ModeId);
        }
        RoiDocumentValue.ValidateShape(shape);
        return RoiDocumentValue.Root(
            RoiDocumentValue.Group(RoiDocumentValue.Explicit,
                new[] { RoiDocumentValue.Item(new Guid("c8a845d3-1695-4761-ac6b-165fe86dc248"), "继承区域", shape) }),
            (local ?? RoiDocumentValue.Empty).Get<RecordValue>(RoiDocumentValue.Mask));
    }

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
            AreaTypeChoices = new[]
            {
                new NativeIntegerRadioChoice(0, "图形类型"),
                new NativeIntegerRadioChoice(1, "图像类型")
            },
            MaskImageFieldId = BlobLabelsSchema.MaskInput,
            MaskImageLabel = "输入掩膜",
            OutputMaskFieldId = OutputMask,
            OutputMaskLabel = "输出掩膜",
            MaskSourceFieldId = BlobLabelsSchema.MaskRegionInput,
            MaskSourceLabel = "屏蔽区",
            MaskInheritedFieldId = MaskInherited,
            AttachAnchorFieldId = BlobLabelsSchema.ImageInput,
            AttachSection = "ROI区域"
        }, attach, observe);
    }
}

/// <summary>BlobLabels 的正式 ROI 文档，复用已验证的公共文档与生命周期。</summary>
internal static class BlobLabelsLocalRoi
{
    public const int MaximumInspectionCount = 16;
    public const int MaximumMaskCount = 8;

    private static IReadOnlyList<NativeRoiEditGroup> Groups { get; } = Array.AsReadOnly(new[]
    {
        new NativeRoiEditGroup(RoiDocumentValue.Inspection, "形状", false,
            MaximumInspectionCount, RoiDocumentValue.FullDomain),
        new NativeRoiEditGroup(RoiDocumentValue.Mask, "屏蔽区", true,
            MaximumMaskCount, RoiDocumentValue.None)
    });

    private static MaskInheritanceSpec MaskInheritance { get; } = new(
        BlobLabelsRoi.MaskInherited, BlobLabelsSchema.MaskRegionInput, RoiDocumentValue.MaximumInheritedCount);

    public static INativeRoiEditDocument Create(DeclaredInputFieldsView inputs) => new RoiDocumentStore(
        inputs, BlobLabelsRoi.LocalDocumentId, Groups, BlobLabelsSchema.ImageInput,
        BlobLabelsRoi.Inheritance, MaskInheritance, new[] { "检测区域", "屏蔽区域" });
}

/// <summary>BlobLabels 原厂 AssistGroup 的模式和来源字段声明。</summary>
public static class BlobLabelsAssist
{
    public const string ModeId = "input.pose.mode";
    public const string BasePoint = "input.pose.base-point";
    public const string RunPoint = "input.pose.run-point";

    public static TypeRef PointType { get; } = TypeRef.RecordOf("bloblabels.pose.point", new[]
    {
        new Port("x", DataType.Number), new Port("y", DataType.Number)
    });

    public static IReadOnlyList<Port> Ports { get; } = new[]
    {
        new Port(ModeId, DataType.Integer, Required: false, EditModes: FieldEditModes.Constant),
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
        var information = inputs.ComposeInputEditor(BlobLabelsSchema.FixtureInput, "修正信息", referenceOnly: true);
        var assist = new NativeAssistUiDraftControls(mode, information, Sources(inputs));
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
