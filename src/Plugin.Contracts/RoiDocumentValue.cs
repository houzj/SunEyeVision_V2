using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Plugin.Contracts;

/// <summary>
/// ROI 文档的标准记录结构：一个参数字段装下整组区域，读者各自反序列化。
/// 四层分别是点、形状、条目、组；形状只允许矩形、扇环、多边形三类。
/// 结构由平台定义，任何工具只要遵循同一结构即可被通用编辑器承载。
/// </summary>
public static class RoiDocumentValue
{
    /// <summary>检测区组名。</summary>
    public const string Inspection = "inspection";
    /// <summary>屏蔽区组名。</summary>
    public const string Mask = "mask";

    public const string None = "None", FullDomain = "FullDomain", Explicit = "Explicit";

    /// <summary>原厂 ROISelecter.MaxPolygonPointNum 同为 32。</summary>
    public const uint MaximumVertices = 32;
    /// <summary>继承模式下单个组允许的最大条目数。</summary>
    public const int MaximumInheritedCount = 64;

    public static TypeRef PointType { get; } = TypeRef.RecordOf("roi.point", new[]
    {
        new Port("x", DataType.Number), new Port("y", DataType.Number)
    });

    public static TypeRef ShapeType { get; } = TypeRef.RecordOf("roi.shape", new[]
    {
        new Port("kind", DataType.Text), new Port("center", PointType, false),
        new Port("width", DataType.Number, false), new Port("height", DataType.Number, false),
        new Port("angle-degrees", DataType.Number, false), new Port("inner-radius", DataType.Number, false),
        new Port("outer-radius", DataType.Number, false), new Port("start-radians", DataType.Number, false),
        new Port("end-radians", DataType.Number, false), new Port("vertices", TypeRef.SequenceOf(PointType), false)
    });

    public static TypeRef ItemType { get; } = TypeRef.RecordOf("roi.item", new[]
    {
        new Port("id", DataType.Text), new Port("name", DataType.Text),
        new Port("enabled", DataType.Boolean), new Port("visible", DataType.Boolean),
        new Port("shape", ShapeType)
    });

    public static TypeRef ItemsType { get; } = TypeRef.SequenceOf(ItemType);

    public static TypeRef GroupType { get; } = TypeRef.RecordOf("roi.group", new[]
    {
        new Port("coverage", DataType.Text), new Port("items", ItemsType)
    });

    public static TypeRef Declaration { get; } = TypeRef.RecordOf("roi.document", new[]
    {
        new Port(Inspection, GroupType), new Port(Mask, GroupType)
    });

    public static RecordValue Empty { get; } = Root(Group(FullDomain, Array.Empty<RecordValue>()), Group(None, Array.Empty<RecordValue>()));

    public static RecordValue Point(double x, double y) => new(PointType, new Dictionary<string, object?>
    {
        ["x"] = x, ["y"] = y
    });

    public static RecordValue Item(Guid id, string name, RecordValue shape, bool enabled = true, bool visible = true)
        => new(ItemType, new Dictionary<string, object?>
        {
            ["id"] = id.ToString("N"), ["name"] = name, ["shape"] = shape, ["enabled"] = enabled, ["visible"] = visible
        });

    public static RecordValue Group(string coverage, IEnumerable<RecordValue> items)
        => new(GroupType, new Dictionary<string, object?>
        {
            ["coverage"] = coverage, ["items"] = new SequenceValue(ItemsType, items)
        });

    public static RecordValue Root(RecordValue inspection, RecordValue mask)
        => new(Declaration, new Dictionary<string, object?> { [Inspection] = inspection, [Mask] = mask });

    public static IReadOnlyList<RecordValue> Items(RecordValue root, string group)
        => root.Get<RecordValue>(group).Get<SequenceValue>("items").Cast<RecordValue>().ToArray();

    public static string Coverage(RecordValue root, string group)
        => root.Get<RecordValue>(group).Get<string>("coverage");

    public static bool Same(object? left, object? right) => left is RecordValue a && right is RecordValue b
        ? a.Declaration == b.Declaration && a.CopyFields().Count == b.CopyFields().Count
            && a.CopyFields().All(pair => b.Contains(pair.Key) && Same(pair.Value, b.Get<object?>(pair.Key)))
        : left is SequenceValue x && right is SequenceValue y
            ? x.Count == y.Count && x.Zip(y).All(pair => Same(pair.First, pair.Second))
            : Equals(left, right);

    public static double NormalizeDegrees(double angle) => Math.IEEERemainder(angle, 360d);

    /// <summary>通用结构校验：声明匹配、覆盖状态一致、条目身份与形状合法、屏蔽区只允许多边形。</summary>
    public static void Validate(RecordValue root)
    {
        if (root.Declaration != Declaration) throw new ArgumentException("区域文档声明不匹配。");
        foreach (var group in new[] { Inspection, Mask })
        {
            var coverage = Coverage(root, group);
            var rows = Items(root, group);
            if (coverage is not (None or FullDomain or Explicit) || group == Mask && coverage == FullDomain)
                throw new ArgumentException("区域覆盖状态无效。");
            if (coverage != Explicit && rows.Count != 0 || coverage == Explicit && rows.Count == 0)
                throw new ArgumentException("显式区域与覆盖状态不一致。");
            var ids = new HashSet<Guid>();
            foreach (var item in rows)
            {
                if (!Guid.TryParseExact(item.Get<string>("id"), "N", out var id) || id == Guid.Empty || !ids.Add(id))
                    throw new ArgumentException("区域身份无效或重复。");
                if (string.IsNullOrWhiteSpace(item.Get<string>("name")))
                    throw new ArgumentException("区域名称不能为空。");
                var shape = item.Get<RecordValue>("shape");
                ValidateShape(shape);
                if (group == Mask)
                {
                    if (shape.Get<string>("kind") != "polygon") throw new ArgumentException("屏蔽区仅支持多边形。");
                    if (shape.Get<SequenceValue>("vertices").Count > MaximumVertices)
                        throw new ArgumentException($"每个屏蔽区最多支持 {MaximumVertices} 个顶点。");
                }
            }
        }
    }

    public static void ValidateShape(RecordValue shape)
    {
        if (shape.Declaration != ShapeType) throw new ArgumentException("区域形状声明不匹配。");
        var kind = shape.Get<string>("kind");
        string[] fields = kind switch
        {
            "rectangle" => new[] { "kind", "center", "width", "height", "angle-degrees" },
            "annulus" or "sector" => new[] { "kind", "center", "inner-radius", "outer-radius", "start-radians", "end-radians" },
            "polygon" => new[] { "kind", "vertices" },
            _ => throw new ArgumentException("不支持的区域形状。")
        };
        if (!shape.CopyFields().Keys.ToHashSet(StringComparer.Ordinal).SetEquals(fields))
            throw new ArgumentException("形状字段不完整或包含其他类型的字段。");
        double Number(string name)
        {
            var value = shape.Get<double>(name);
            if (!double.IsFinite(value)) throw new ArgumentException("形状参数必须有限。");
            return value;
        }
        void PointChecked(RecordValue point)
        {
            if (point.Declaration != PointType || !double.IsFinite(point.Get<double>("x")) || !double.IsFinite(point.Get<double>("y")))
                throw new ArgumentException("区域坐标必须有限。");
        }
        if (kind == "polygon")
        {
            var points = shape.Get<SequenceValue>("vertices").Cast<RecordValue>().ToArray();
            foreach (var point in points) PointChecked(point);
            if (points.Select(point => (point.Get<double>("x"), point.Get<double>("y"))).Distinct().Count() < 3)
                throw new ArgumentException("多边形至少需要三个不同顶点。");
            return;
        }
        PointChecked(shape.Get<RecordValue>("center"));
        if (kind == "rectangle")
        {
            if (Number("width") / 2 <= 0 || Number("height") / 2 <= 0) throw new ArgumentException("矩形尺寸必须为正。");
            _ = Number("angle-degrees");
            return;
        }
        if (Number("inner-radius") < 0 || Number("outer-radius") <= Number("inner-radius"))
            throw new ArgumentException("圆环半径无效。");
        var sweep = Number("end-radians") - Number("start-radians");
        if (!double.IsFinite(sweep) || Math.Abs(sweep) < 1e-12 || Math.Abs(sweep) > 2 * Math.PI + 1e-9 || kind == "sector" && sweep <= 0)
            throw new ArgumentException("形状角度范围无效。");
    }
}
