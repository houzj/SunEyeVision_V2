using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// 几何定义（RoiShape.Geometry）为多态 <see cref="object"/> 字段，
/// System.Text.Json 序列化后无法还原具体几何类型（反序列化为 JsonElement）。
/// 本转换器在序列化时写入 <c>$geom</c> 判别符（几何类型短名），反序列化据此还原具体类型。
/// </summary>
/// <remarks>
/// 通过 <c>[JsonConverter]</c> 挂到几何字段上，作用域限于该字段。
/// 未知 <c>$geom</c> 直接抛 <see cref="NotSupportedException"/>（rule-015 不静默）。
///
/// 几何值一律为 <c>Models.Geometry</c> 契约类型；历史工程中的 UI/WPF 类型
/// （<c>Point2D</c>、<c>System.Windows.Rect</c>）在读入时迁移为契约类型（见 <see cref="Read"/>）。
/// </remarks>
public class GeometryJsonConverter : JsonConverter<object?>
{
    private static readonly Dictionary<string, Type> GeometryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Circle2d"]      = typeof(Circle2d),
        ["RotatedRect2d"] = typeof(RotatedRect2d),
        ["Annulus2d"]     = typeof(Annulus2d),
        ["Polygon2d"]     = typeof(Polygon2d),
        ["Line2d"]        = typeof(Line2d),
        ["Arc2d"]         = typeof(Arc2d),
        ["Point2d"]       = typeof(Point2d),
        // 存量数据迁移：历史工程以 UI 交互类型 Point2D 承载点图形（X/Y 结构与 Point2d 一致），
        // 读入时统一还原为契约类型 Point2d。WPF Rect 结构不同，在 Read 中单独转换。
        ["Point2D"]       = typeof(Point2d),
    };

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new NotSupportedException("几何数据格式错误：期望 JSON 对象");

        if (!root.TryGetProperty("$geom", out var geomEl))
            throw new NotSupportedException("几何数据缺少 $geom 判别符，无法反序列化（旧数据或格式错误）");

        var geomTypeName = geomEl.GetString();

        // 存量数据迁移：历史工程以 WPF Rect 承载轴对齐矩形，读入时转换为契约类型 RotatedRect2d
        if (string.Equals(geomTypeName, "Rect", StringComparison.OrdinalIgnoreCase))
            return ReadLegacyRect(root);

        if (geomTypeName == null || !GeometryTypes.TryGetValue(geomTypeName, out var geomType))
            throw new NotSupportedException($"未知几何类型: {geomTypeName}");

        // 去掉 $geom 判别符后以具体类型反序列化（避免递归套用本 object? 转换器）
        var payload = RemoveDiscriminator(root);
        return JsonSerializer.Deserialize(payload, geomType, options);
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("$geom", value.GetType().Name);
        // 以具体类型序列化（不套用本 object? 转换器，避免递归），把其余属性展开到同一层
        var json = JsonSerializer.Serialize(value, value.GetType(), options);
        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            prop.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    /// <summary>
    /// 存量迁移：旧 WPF Rect 几何（左上角 X/Y + 宽高）→ <see cref="RotatedRect2d"/>（中心 + 宽高 + 角度 0）。
    /// </summary>
    private static RotatedRect2d ReadLegacyRect(JsonElement root)
    {
        var x = root.GetProperty("X").GetDouble();
        var y = root.GetProperty("Y").GetDouble();
        var width = root.GetProperty("Width").GetDouble();
        var height = root.GetProperty("Height").GetDouble();
        return new RotatedRect2d(x + width / 2, y + height / 2, width, height, 0);
    }

    /// <summary>
    /// 复制对象 JSON，剔除 <c>$geom</c> 判别符（具体类型反序列化时不需要它）。
    /// </summary>
    private static string RemoveDiscriminator(JsonElement root)
    {
        var buffer = new System.Text.StringBuilder();
        buffer.Append('{');
        var first = true;
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name == "$geom")
                continue;
            if (!first)
                buffer.Append(',');
            first = false;
            buffer.Append('"').Append(prop.Name).Append("\":").Append(prop.Value.GetRawText());
        }
        buffer.Append('}');
        return buffer.ToString();
    }
}
