using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 轮廓匹配基本参数页「ROI区域」的正式字段体系，按原厂 IMVSContourMatchModuAlgorithmTab.xml:9-41
/// Tab_ROI Area 的 GroupLinkItem 结构（OutputMaskImage/InputMaskImage/RoiSelect/ROISelecter/
/// InheritWay/ExternROIType/Assist）与 PixelCount/Blob 已验证的平台 ROI 体系同构实现。
///
/// 中立命名；上游 Region 输入仍由 ContourMatchSchema.RoiInput（原厂 InputROI）承担，二者并存：
/// - 本类字段 = 平台 ROI 组合的本地绘制/形状参数继承/掩膜语义（UI 对齐原厂 Tab_ROI Area）
/// - ContourMatchSearchRois = 运行时搜索域二选一（Local 本地文档 / Upstream 上游 Region 连线）
/// 本地文档字段与 ContourMatchSearchRois.LocalId 共用 input.roi.local。
/// </summary>
public static class ContourMatchRoi
{
    public const string ModeId = "input.roi.inheritance-mode";
    public const string Local = "Local";
    public const string Rectangle = "Rectangle";
    public const string RectangleParameters = "RectangleParameters";
    public const string Annulus = "Annulus";
    public const string AnnulusParameters = "AnnulusParameters";
    public const string RectangleSource = "input.roi.rectangle";
    public const string AnnulusSource = "input.roi.annulus";
    public const string AreaType = "input.roi.area-type";
    public const string MaskImage = "input.roi.mask-image";
    public const string OutputMask = "input.roi.output-mask";
    public const string MaskSource = "input.roi.mask-source";
    public const string MaskInherited = "input.roi.mask-inherited";

    public const string LocalDocumentId = "input.roi.local";
    public const int MaximumInherited = RoiDocumentValue.MaximumInheritedCount;

    public static RoiInheritanceSpec Inheritance { get; } = new(
        ModeId, Local,
        new(Rectangle, RectangleParameters, RectangleSource,
            new[] { "center-x", "center-y", "width", "height", "angle-degrees" }, "rectangle"),
        new(Annulus, AnnulusParameters, AnnulusSource,
            new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" }, "annulus"),
        MaximumInherited);

    /// <summary>结果视图与参数页共用的 ROI 编辑声明（原件 ROISelecter 常驻语义）。</summary>
    public static RoiEditDeclaration RoiEdit { get; } = new(
        ContourMatchSearchRois.LocalId, ContourMatchSchema.ImageInput,
        new[]
        {
            new RoiEditGroupDeclaration(RoiDocumentValue.Inspection, "形状", false,
                ContourMatchSearchRois.MaximumLocalInspectionCount, RoiDocumentValue.FullDomain),
            new RoiEditGroupDeclaration(RoiDocumentValue.Mask, "屏蔽区", true,
                ContourMatchSearchRois.MaximumLocalMaskCount, RoiDocumentValue.None)
        },
        Inheritance: Inheritance,
        MaskInheritance: new RoiMaskInheritanceDeclaration(MaskInherited, MaskSource, MaximumInherited),
        ItemNames: new[] { "检测区域", "屏蔽区域" });
    private static readonly Guid InheritedItemId = new("c8a845d3-1695-4761-ac6b-165fe86dc248");

    public static TypeRef RectangleType { get; } = TypeRef.RecordOf("contourmatch.roi.rectangle", new[]
    {
        new Port("center-x", DataType.Number), new Port("center-y", DataType.Number),
        new Port("width", DataType.Number), new Port("height", DataType.Number),
        new Port("angle-degrees", DataType.Number)
    });
    public static TypeRef AnnulusType { get; } = TypeRef.RecordOf("contourmatch.roi.annulus", new[]
    {
        new Port("center-x", DataType.Number), new Port("center-y", DataType.Number),
        new Port("inner-radius", DataType.Number), new Port("outer-radius", DataType.Number),
        new Port("start-degrees", DataType.Number), new Port("sweep-degrees", DataType.Number)
    });
    public static TypeRef PolygonStrings { get; } = TypeRef.SequenceOf(DataType.Text);

    /// <summary>ROI 相关输入字段；本地文档字段由 ContourMatchSearchRois.LocalPort 声明，此处不重复。</summary>
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

    public static string ReadMode(FieldSettings fields)
        => fields.Declarations.ContainsKey(ModeId)
            ? fields.Read(ModeId) switch
            {
                ConstantSetting { Value: string mode } => ValidateMode(mode),
                UnsetSetting => Local,
                _ => throw new ArgumentException("ROI继承方式必须是固定模式。")
            }
            : Local;

    public static string ReadMode(InputValues inputs)
        => inputs.Contains(ModeId)
            ? inputs.Get<string>(ModeId) is { } mode ? ValidateMode(mode) : Local
            : Local;

    public static string ValidateMode(string mode)
        => mode is Local or Rectangle or RectangleParameters or Annulus or AnnulusParameters
            ? mode
            : throw new ArgumentException("ROI继承方式无效：" + mode);

    public static bool IsLocalMode(string mode)
        => mode is Local or Rectangle or RectangleParameters or Annulus or AnnulusParameters;

    /// <summary>
    /// 将原厂单 ROI 的“按区域/按参数”字段物化为平台 ROI 文档。
    /// ContourMatch 的原厂端口是单个 Record/Scalar；不能直接套用 Blob 的多 ROI Sequence/NumberArray 合同。
    /// </summary>
    public static RecordValue Materialize(InputValues inputs, RecordValue? local)
    {
        var mode = ReadMode(inputs);
        var mask = (local ?? RoiDocumentValue.Empty).Get<RecordValue>(RoiDocumentValue.Mask);
        if (mode == Local) return MaterializeMaskInheritance(inputs, local ?? RoiDocumentValue.Empty);

        RecordValue shape;
        if (mode == Rectangle)
        {
            if (!inputs.Contains(RectangleSource))
                throw new ArgumentException("请选择完整的矩形 ROI 继承来源。");
            shape = RectangleShape(inputs.Get<RecordValue>(RectangleSource));
        }
        else if (mode == RectangleParameters)
        {
            shape = RectangleShape(
                Number(inputs, RectangleSource + ".center-x"),
                Number(inputs, RectangleSource + ".center-y"),
                Number(inputs, RectangleSource + ".width"),
                Number(inputs, RectangleSource + ".height"),
                Number(inputs, RectangleSource + ".angle-degrees"));
        }
        else if (mode == Annulus)
        {
            if (!inputs.Contains(AnnulusSource))
                throw new ArgumentException("请选择完整的圆环 ROI 继承来源。");
            shape = AnnulusShape(inputs.Get<RecordValue>(AnnulusSource));
        }
        else
        {
            shape = AnnulusShape(
                Number(inputs, AnnulusSource + ".center-x"),
                Number(inputs, AnnulusSource + ".center-y"),
                Number(inputs, AnnulusSource + ".inner-radius"),
                Number(inputs, AnnulusSource + ".outer-radius"),
                Number(inputs, AnnulusSource + ".start-degrees"),
                Number(inputs, AnnulusSource + ".sweep-degrees"));
        }

        var item = RoiDocumentValue.Item(InheritedItemId, "继承区域", shape);
        return MaterializeMaskInheritance(inputs,
            RoiDocumentValue.Root(RoiDocumentValue.Group(RoiDocumentValue.Explicit, new[] { item }), mask));
    }

    public static IReadOnlyList<string> ActiveInputs(FieldSettings fields, IEnumerable<Port> inputs)
    {
        var mode = ReadMode(fields);
        var own = Ports.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal) { ModeId };
        if (mode == Rectangle) active.Add(RectangleSource);
        else if (mode == RectangleParameters)
            foreach (var member in new[] { "center-x", "center-y", "width", "height", "angle-degrees" })
                active.Add(RectangleSource + "." + member);
        else if (mode == Annulus) active.Add(AnnulusSource);
        else if (mode == AnnulusParameters)
            foreach (var member in new[] { "center-x", "center-y", "inner-radius", "outer-radius", "start-degrees", "sweep-degrees" })
                active.Add(AnnulusSource + "." + member);
        // 屏蔽区继承是运行期搜索域的一部分，必须随执行快照进入 Runtime；不能只留在
        // NativeRoiGroupComposer 的预览字段里。
        active.Add(MaskInherited);
        if (fields.Read(MaskInherited) is ConstantSetting { Value: true }) active.Add(MaskSource);
        return Array.AsReadOnly(inputs.Where(port => !own.Contains(port.Id) || active.Contains(port.Id))
            .Select(port => port.Id).ToArray());
    }

    private static RecordValue MaterializeMaskInheritance(InputValues inputs, RecordValue document)
    {
        if (!inputs.Contains(MaskInherited) || inputs.Get<bool>(MaskInherited) is not true) return document;
        if (!inputs.Contains(MaskSource)) throw new ArgumentException("已启用屏蔽区继承，但没有屏蔽区坐标来源。");
        var source = inputs.Get<SequenceValue>(MaskSource);
        if (source.Count > MaximumInherited) throw new ArgumentException($"继承屏蔽区最多支持 {MaximumInherited} 个多边形。");
        var items = source.Cast<string>().Select((text, index) =>
        {
            if (text.Length > 1023) throw new ArgumentException($"继承屏蔽区 {index + 1} 的坐标文本超过长度上限。");
            var values = text.Split(',');
            if (values.Length < 6 || values.Length > RoiDocumentValue.MaximumVertices * 2 || values.Length % 2 != 0)
                throw new ArgumentException($"继承屏蔽区 {index + 1} 需要 3 至 {RoiDocumentValue.MaximumVertices} 对坐标。");
            var points = new List<object?>(values.Length / 2);
            for (var point = 0; point < values.Length; point += 2)
            {
                if (!double.TryParse(values[point], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    || !double.TryParse(values[point + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                    || !double.IsFinite(x) || !double.IsFinite(y))
                    throw new ArgumentException($"继承屏蔽区 {index + 1} 包含无效坐标。");
                points.Add(RoiDocumentValue.Point(x, y));
            }
            var shape = new RecordValue(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
            {
                ["kind"] = "polygon", ["vertices"] = new SequenceValue(TypeRef.SequenceOf(RoiDocumentValue.PointType), points)
            });
            return RoiDocumentValue.Item(new Guid(0x00a9ef00 + index, 0x6985, 0x4e44, 0xa8, 0x82, 0xf9, 0x16, 0x75, 0x2a, 0x53, 0x91),
                "继承屏蔽区" + (index + 1), shape);
        }).ToArray();
        var inspection = document.Get<RecordValue>(RoiDocumentValue.Inspection);
        return RoiDocumentValue.Root(inspection,
            RoiDocumentValue.Group(items.Length == 0 ? RoiDocumentValue.None : RoiDocumentValue.Explicit, items));
    }

    private static double Number(InputValues inputs, string id)
    {
        var value = inputs.Get<double>(id);
        if (!double.IsFinite(value)) throw new ArgumentException("ROI继承参数必须有限。");
        return value;
    }

    private static RecordValue RectangleShape(RecordValue source)
        => RectangleShape(source.Get<double>("center-x"), source.Get<double>("center-y"),
            source.Get<double>("width"), source.Get<double>("height"), source.Get<double>("angle-degrees"));

    private static RecordValue RectangleShape(double centerX, double centerY, double width, double height, double angle)
        => new(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
        {
            ["kind"] = "rectangle", ["center"] = RoiDocumentValue.Point(centerX, centerY),
            ["width"] = width, ["height"] = height, ["angle-degrees"] = angle
        });

    private static RecordValue AnnulusShape(RecordValue source)
        => AnnulusShape(source.Get<double>("center-x"), source.Get<double>("center-y"),
            source.Get<double>("inner-radius"), source.Get<double>("outer-radius"),
            source.Get<double>("start-degrees"), source.Get<double>("sweep-degrees"));

    private static RecordValue AnnulusShape(double centerX, double centerY, double inner, double outer, double start, double sweep)
    {
        var startRadians = RoiDocumentValue.NormalizeDegrees(start) * Math.PI / 180d;
        return new(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
        {
            ["kind"] = "annulus", ["center"] = RoiDocumentValue.Point(centerX, centerY),
            ["inner-radius"] = inner, ["outer-radius"] = outer,
            ["start-radians"] = startRadians, ["end-radians"] = startRadians + sweep * Math.PI / 180d
        });
    }
}
