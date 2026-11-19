using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>
/// 多边形屏蔽区的继承与发布：坐标文本 ↔ 多边形之间互转，含位姿矩阵变换。
/// 工具只提供字段身份与数量上限，解析、校验、编码都在这里。
/// </summary>
public static class PolygonStringInheritance
{
    /// <summary>坐标文本的长度上限，防止单个屏蔽区撑爆订阅载荷。</summary>
    public const int MaximumPayloadLength = 1023;

    /// <summary>把「逗号分隔的 x,y 序列」解析成多边形形状记录，并替换 mask 组。</summary>
    public static RecordValue Materialize(InputValues inputs, string modeFieldId, string sourceFieldId,
        int maximumInherited, int maximumVertices, RecordValue? roi)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (!inputs.Contains(modeFieldId) || !inputs.Get<bool>(modeFieldId)) return roi ?? RoiDocumentValue.Empty;
        var source = inputs.Get<SequenceValue>(sourceFieldId);
        if (source.Count > maximumInherited)
            throw new ArgumentException($"继承屏蔽区最多支持 {maximumInherited} 个多边形。");
        var rows = source.Cast<string>().Select((polygon, index) =>
        {
            var id = new Guid(0x00a9ef00 + index, 0x6985, 0x4e44, 0xa8, 0x82, 0xf9, 0x16, 0x75, 0x2a, 0x53, 0x91);
            var vertices = new SequenceValue(TypeRef.SequenceOf(RoiDocumentValue.PointType),
                ReadVertices(polygon, index + 1, maximumVertices));
            var shape = new RecordValue(RoiDocumentValue.ShapeType,
                new Dictionary<string, object?> { ["kind"] = "polygon", ["vertices"] = vertices });
            RoiDocumentValue.ValidateShape(shape);
            return RoiDocumentValue.Item(id, "继承屏蔽区" + (index + 1), shape);
        }).ToArray();
        var inspection = (roi ?? RoiDocumentValue.Empty).Get<RecordValue>(RoiDocumentValue.Inspection);
        return RoiDocumentValue.Root(inspection,
            RoiDocumentValue.Group(rows.Length == 0 ? RoiDocumentValue.None : RoiDocumentValue.Explicit, rows));
    }

    /// <summary>多边形几何 → 坐标文本，保留 6 位小数。</summary>
    public static string Encode(PolygonGeometry polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        var payload = string.Join(",", polygon.Vertices.SelectMany(point => new[] { Format(point.X), Format(point.Y) }));
        if (payload.Length > MaximumPayloadLength) throw new ArgumentException("屏蔽区坐标文本超过订阅长度上限。");
        return payload;

        static string Format(double coordinate)
        {
            var value = (float)coordinate;
            if (!float.IsFinite(value)) throw new ArgumentException("屏蔽区坐标超出单精度范围。");
            return value.ToString("F6", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>把已启用的多边形屏蔽区发布成平台几何；给定位姿时先做仿射变换。</summary>
    public static SequenceValue Publish(RecordValue? roi, RecordValue? pose)
    {
        var polygonsType = TypeRef.SequenceOf(TypeRef.GeometryOf(GeometryKind.Polygon, RoiDeclarations.ImageFrame));
        var rows = RoiDocumentValue.Items(roi ?? RoiDocumentValue.Empty, RoiDocumentValue.Mask);
        var matrix = pose is null
            ? null
            : new[] { "m11", "m12", "m21", "m22", "offset-x", "offset-y" }.Select(pose.Get<double>).ToArray();
        Point2 Point(RecordValue value)
        {
            var x = value.Get<double>("x");
            var y = value.Get<double>("y");
            return matrix is null ? new(x, y) : new(x * matrix[0] + y * matrix[2] + matrix[4], x * matrix[1] + y * matrix[3] + matrix[5]);
        }
        return new SequenceValue(polygonsType, rows.Where(row => row.Get<bool>("enabled"))
            .Select(row => row.Get<RecordValue>("shape"))
            .Where(shape => shape.Get<string>("kind") == "polygon")
            .Select(shape => (object?)new PolygonGeometry(RoiDeclarations.ImageFrame,
                shape.Get<SequenceValue>("vertices").Cast<RecordValue>().Select(Point))));
    }

    /// <summary>声明的多边形序列类型，供工具复用。</summary>
    public static TypeRef PolygonsType { get; }
        = TypeRef.SequenceOf(TypeRef.GeometryOf(GeometryKind.Polygon, RoiDeclarations.ImageFrame));

    private static IEnumerable<RecordValue> ReadVertices(string polygon, int index, int maximumVertices)
    {
        if (polygon.Length > MaximumPayloadLength)
            throw new ArgumentException($"继承屏蔽区 {index} 的坐标文本超过长度上限。");
        var coordinates = polygon.Split(',');
        if (coordinates.Length < 6 || coordinates.Length > maximumVertices * 2 || coordinates.Length % 2 != 0)
            throw new ArgumentException($"继承屏蔽区 {index} 需要 3 至 {maximumVertices} 对坐标。");
        for (var i = 0; i < coordinates.Length; i += 2)
            yield return RoiDocumentValue.Point(Parse(coordinates[i]), Parse(coordinates[i + 1]));

        float Parse(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && float.IsFinite((float)number)) return (float)number;
            throw new ArgumentException($"继承屏蔽区 {index} 包含无效坐标。");
        }
    }
}
