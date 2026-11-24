using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 模板训练样本集的载荷编解码（每模板独立的样本区域，等价原厂 ModelItem.modelData.ItemsData）。
/// 只序列化建模板需要的几何与身份（名称/正负/启用/形状），不序列化像素与运行时资源。
/// </summary>
public static class TemplateTrainingCodec
{
    private const string ItemsKey = "items";
    private const string NameKey = "name";
    private const string PositiveKey = "positive";
    private const string EnabledKey = "enabled";
    private const string KindKey = "kind";
    private const string CenterKey = "center";
    private const string WidthKey = "width";
    private const string HeightKey = "height";
    private const string AngleKey = "angle";
    private const string PointsKey = "points";

    /// <summary>RoiDocument → 载荷文本；空文档返回空串（不写无意义 JSON）。</summary>
    public static string Encode(RecordValue? document)
    {
        if (document is null) return "";
        var items = new List<(RecordValue Shape, string Name, bool Positive, bool Enabled)>();
        foreach (var item in RoiDocumentValue.Items(document, RoiDocumentValue.Inspection))
            items.Add((item.Get<RecordValue>("shape"), item.Get<string?>("name") ?? "", true, item.Get<bool?>("enabled") ?? true));
        foreach (var item in RoiDocumentValue.Items(document, RoiDocumentValue.Mask))
            items.Add((item.Get<RecordValue>("shape"), item.Get<string?>("name") ?? "", false, item.Get<bool?>("enabled") ?? true));
        if (items.Count == 0) return "";
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartArray(ItemsKey);
            foreach (var (shape, name, positive, enabled) in items)
            {
                var kind = shape.Get<string>("kind");
                writer.WriteStartObject();
                writer.WriteString(NameKey, name);
                writer.WriteBoolean(PositiveKey, positive);
                writer.WriteBoolean(EnabledKey, enabled);
                writer.WriteString(KindKey, kind);
                if (kind == "polygon")
                {
                    writer.WriteStartArray(PointsKey);
                    foreach (var point in shape.Get<SequenceValue>("vertices").Cast<RecordValue>())
                    {
                        writer.WriteStartArray();
                        writer.WriteNumberValue(point.Get<double>("x"));
                        writer.WriteNumberValue(point.Get<double>("y"));
                        writer.WriteEndArray();
                    }
                    writer.WriteEndArray();
                }
                else
                {
                    var center = shape.Get<RecordValue>(CenterKey);
                    writer.WriteStartArray(CenterKey);
                    writer.WriteNumberValue(center.Get<double>("x"));
                    writer.WriteNumberValue(center.Get<double>("y"));
                    writer.WriteEndArray();
                    writer.WriteNumber(WidthKey, shape.Get<double?>(WidthKey) ?? 0);
                    writer.WriteNumber(HeightKey, shape.Get<double?>(HeightKey) ?? 0);
                    writer.WriteNumber(AngleKey, shape.Get<double?>("angle-degrees") ?? 0);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>载荷文本 → RoiDocument；空文本返回空文档。</summary>
    public static RecordValue Decode(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return RoiDocumentValue.Empty;
        using var document = JsonDocument.Parse(payload);
        var positive = new List<RecordValue>();
        var background = new List<RecordValue>();
        if (document.RootElement.TryGetProperty(ItemsKey, out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var name = item.TryGetProperty(NameKey, out var nameNode) ? nameNode.GetString() ?? "" : "";
                var isPositive = !item.TryGetProperty(PositiveKey, out var positiveNode) || positiveNode.ValueKind != JsonValueKind.False;
                var enabled = !item.TryGetProperty(EnabledKey, out var enabledNode) || enabledNode.ValueKind != JsonValueKind.False;
                var kind = item.TryGetProperty(KindKey, out var kindNode) ? kindNode.GetString() ?? "rectangle" : "rectangle";
                RecordValue shape;
                if (kind == "polygon")
                {
                    var points = (item.GetProperty(PointsKey)).EnumerateArray()
                        .Select(point => new Point2(point[0].GetDouble(), point[1].GetDouble())).ToArray();
                    if (points.Length < 3) continue;
                    shape = DocumentPolygon(points);
                }
                else
                {
                    var centerNode = item.GetProperty(CenterKey);
                    var cx = centerNode[0].GetDouble();
                    var cy = centerNode[1].GetDouble();
                    var width = item.GetProperty(WidthKey).GetDouble();
                    var height = item.GetProperty(HeightKey).GetDouble();
                    shape = DocumentRectangle(cx, cy, width, height,
                        item.TryGetProperty(AngleKey, out var angleNode) ? angleNode.GetDouble() : 0d);
                }
                var entry = RoiDocumentValue.Item(Guid.NewGuid(), name, shape, enabled);
                (isPositive ? positive : background).Add(entry);
            }
        }
        return RoiDocumentValue.Root(
            // Keep the group coverage semantics when the payload is round-tripped.
            // An empty positive group is the declared full-domain state, while an
            // empty background group is None.  Marking either empty group Explicit
            // makes the second Create/Match pass fail RoiDocumentValue.Validate
            // with "显式区域与覆盖状态不一致".
            RoiDocumentValue.Group(positive.Count == 0 ? RoiDocumentValue.FullDomain : RoiDocumentValue.Explicit, positive),
            RoiDocumentValue.Group(background.Count == 0 ? RoiDocumentValue.None : RoiDocumentValue.Explicit, background));
    }

    private static RecordValue DocumentRectangle(double centerX, double centerY, double width, double height, double angle)
        => new(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
        {
            [KindKey] = "rectangle",
            [CenterKey] = RoiDocumentValue.Point(centerX, centerY),
            [WidthKey] = width,
            [HeightKey] = height,
            ["angle-degrees"] = angle
        });

    private static RecordValue DocumentPolygon(IReadOnlyList<Point2> points)
        => new(RoiDocumentValue.ShapeType, new Dictionary<string, object?>
        {
            [KindKey] = "polygon",
            ["vertices"] = new SequenceValue(TypeRef.SequenceOf(RoiDocumentValue.PointType),
                points.Select(point => (object?)RoiDocumentValue.Point(point.X, point.Y)))
        });
}
