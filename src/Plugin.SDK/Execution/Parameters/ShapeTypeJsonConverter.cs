using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// <see cref="ShapeType"/> 读写转换器：承担历史数据迁移。
/// 存量工程以数值 4 表示已删除的 <c>Rectangle</c>，读入时迁移为 <see cref="ShapeType.RotatedRectangle"/>
/// （矩形统一为可旋转矩形）。未知类型按 rule-015 直接抛异常，不静默回退。
/// </summary>
public class ShapeTypeJsonConverter : JsonConverter<ShapeType>
{
    /// <summary>历史 Rectangle 的数值（枚举中已删除，仅用于存量数据迁移）</summary>
    private const int LegacyRectangleValue = 4;

    public override ShapeType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (string.Equals(text, "Rectangle", StringComparison.OrdinalIgnoreCase))
                return ShapeType.RotatedRectangle;

            if (Enum.TryParse<ShapeType>(text, ignoreCase: true, out var parsed))
                return parsed;

            throw new NotSupportedException($"未知形状类型: {text}");
        }

        var value = reader.GetInt32();
        if (value == LegacyRectangleValue)
            return ShapeType.RotatedRectangle;

        if (Enum.IsDefined(typeof(ShapeType), value))
            return (ShapeType)value;

        throw new NotSupportedException($"未知形状类型数值: {value}");
    }

    public override void Write(Utf8JsonWriter writer, ShapeType value, JsonSerializerOptions options)
        => writer.WriteNumberValue((int)value);
}
