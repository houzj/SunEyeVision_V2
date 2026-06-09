using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using VisionLogger = SunEyeVision.Plugin.SDK.Logging.VisionLogger;
using VisionLogLevel = SunEyeVision.Plugin.SDK.Logging.LogLevel;

namespace SunEyeVision.Workflow;

/// <summary>
/// 全局变量值转换器 - 保留类型信息的序列化/反序列化
/// </summary>
/// <remarks>
/// 设计原则（rule-010: 方案系统实现规范）：
/// - 弱类型 + 类型字符串（视觉软件行业标准）
/// - JSON格式直观易读：{ "Type": "Integer", "Value": 128 }
/// - 支持15种类型：基础类型+几何类型+图像+区域
///
/// 支持的数据类型：
/// 1. 基础类型：Integer, String, Boolean, Double, Byte
/// 2. OpenCvSharp类型：Point, Rectangle, Size
/// 3. 图像类型：Image（Mat序列化）
/// 4. 几何类型：Circle, Line, Annulus, RotatedRect, Polygon
/// 5. 区域：Region（仅运行时，不支持序列化）
/// 
/// 设计理念：
/// - 与VisionMaster、Halcon等行业软件保持一致
/// - 不使用多态，使用弱类型 + 类型字符串
/// - JSON格式清晰易读，便于调试和维护
/// </remarks>
public class GlobalVariableValueConverter : JsonConverter<object?>
{
    /// <summary>
    /// 支持的类型常量
    /// </summary>
    public static class SupportedTypes
    {
        public const string Integer = "Integer";
        public const string String = "String";
        public const string Boolean = "Boolean";
        public const string Double = "Double";
        public const string Byte = "Byte";
        public const string Point = "Point";
        public const string Rectangle = "Rectangle";
        public const string Size = "Size";
        public const string Image = "Image";
        public const string Region = "Region";
        public const string Circle = "Circle";
        public const string Line = "Line";
        public const string RotatedRect = "RotatedRect";
        public const string Polygon = "Polygon";
        public const string Annulus = "Annulus";
    }

    /// <summary>
    /// 反序列化
    /// </summary>
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        // 读取类型和值
        if (!root.TryGetProperty("Type", out var typeElement))
        {
            VisionLogger.Instance.Log(VisionLogLevel.Warning, 
                "全局变量值缺少Type字段，返回null", 
                "GlobalVariableValueConverter");
            return null;
        }

        var typeName = typeElement.GetString() ?? "";
        if (!root.TryGetProperty("Value", out var valueElement))
        {
            VisionLogger.Instance.Log(VisionLogLevel.Warning, 
                $"全局变量值缺少Value字段，类型={typeName}", 
                "GlobalVariableValueConverter");
            return null;
        }

        // 根据类型解析值
        try
        {
            var result = ParseValue(typeName, valueElement, options);
            VisionLogger.Instance.Log(VisionLogLevel.Info, 
                $"反序列化全局变量值成功: Type={typeName}, Value={result}", 
                "GlobalVariableValueConverter");
            return result;
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(VisionLogLevel.Error, 
                $"反序列化全局变量值失败: Type={typeName}, 错误={ex.Message}", 
                "GlobalVariableValueConverter", ex);
            return null;
        }
    }

    /// <summary>
    /// 序列化
    /// </summary>
    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value == null)
        {
            writer.WriteString("Type", "String");
            writer.WriteNull("Value");
        }
        else
        {
            var (typeName, serializedValue) = SerializeValue(value, options);
            writer.WriteString("Type", typeName);
            writer.WritePropertyName("Value");
            JsonSerializer.Serialize(writer, serializedValue, options);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// 解析值
    /// </summary>
    private object? ParseValue(string typeName, JsonElement valueElement, JsonSerializerOptions options)
    {
        return typeName switch
        {
            SupportedTypes.Integer => valueElement.GetInt32(),
            SupportedTypes.String => valueElement.GetString(),
            SupportedTypes.Boolean => valueElement.GetBoolean(),
            SupportedTypes.Double => valueElement.GetDouble(),
            SupportedTypes.Byte => valueElement.GetByte(),
            SupportedTypes.Point => ParsePoint(valueElement),
            SupportedTypes.Rectangle => ParseRectangle(valueElement),
            SupportedTypes.Size => ParseSize(valueElement),
            SupportedTypes.Image => ParseImage(valueElement),
            SupportedTypes.Region => throw new NotSupportedException("区域类型不能序列化为JSON"),
            SupportedTypes.Circle => ParseCircle(valueElement),
            SupportedTypes.Line => ParseLine(valueElement),
            SupportedTypes.RotatedRect => ParseRotatedRect(valueElement),
            SupportedTypes.Polygon => ParsePolygon(valueElement),
            SupportedTypes.Annulus => ParseAnnulus(valueElement),
            _ => throw new NotSupportedException($"不支持的类型: {typeName}")
        };
    }

    /// <summary>
    /// 序列化值
    /// </summary>
    private (string TypeName, object? SerializedValue) SerializeValue(object value, JsonSerializerOptions options)
    {
        return value switch
        {
            int intValue => (SupportedTypes.Integer, intValue),
            string stringValue => (SupportedTypes.String, stringValue),
            bool boolValue => (SupportedTypes.Boolean, boolValue),
            double doubleValue => (SupportedTypes.Double, doubleValue),
            byte byteValue => (SupportedTypes.Byte, byteValue),
            Point point => (SupportedTypes.Point, SerializePoint(point)),
            Rect rect => (SupportedTypes.Rectangle, SerializeRectangle(rect)),
            Size size => (SupportedTypes.Size, SerializeSize(size)),
            Mat mat => (SupportedTypes.Image, SerializeImage(mat)),
            IVisionRegion => throw new NotSupportedException("区域类型不能序列化为JSON"),
            Circle2d circle => (SupportedTypes.Circle, SerializeCircle(circle)),
            Line2d line => (SupportedTypes.Line, SerializeLine(line)),
            Annulus2d annulus => (SupportedTypes.Annulus, SerializeAnnulus(annulus)),
            _ => (SupportedTypes.String, value.ToString())
        };
    }

    #region Point 序列化

    private Point ParsePoint(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            // 格式: "X,Y"
            var str = element.GetString() ?? "0,0";
            var parts = str.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
            {
                return new Point(x, y);
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            // 格式: { "X": 100, "Y": 200 }
            var x = element.GetProperty("X").GetInt32();
            var y = element.GetProperty("Y").GetInt32();
            return new Point(x, y);
        }

        return new Point(0, 0);
    }

    private Dictionary<string, int> SerializePoint(Point point)
    {
        return new Dictionary<string, int>
        {
            ["X"] = point.X,
            ["Y"] = point.Y
        };
    }

    #endregion

    #region Rectangle 序列化

    private Rect ParseRectangle(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            // 格式: "X,Y,Width,Height"
            var str = element.GetString() ?? "0,0,0,0";
            var parts = str.Split(',');
            if (parts.Length == 4 &&
                int.TryParse(parts[0], out var x) &&
                int.TryParse(parts[1], out var y) &&
                int.TryParse(parts[2], out var width) &&
                int.TryParse(parts[3], out var height))
            {
                return new Rect(x, y, width, height);
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            // 格式: { "X": 10, "Y": 20, "Width": 100, "Height": 50 }
            var x = element.GetProperty("X").GetInt32();
            var y = element.GetProperty("Y").GetInt32();
            var width = element.GetProperty("Width").GetInt32();
            var height = element.GetProperty("Height").GetInt32();
            return new Rect(x, y, width, height);
        }

        return new Rect(0, 0, 0, 0);
    }

    private Dictionary<string, int> SerializeRectangle(Rect rect)
    {
        return new Dictionary<string, int>
        {
            ["X"] = rect.X,
            ["Y"] = rect.Y,
            ["Width"] = rect.Width,
            ["Height"] = rect.Height
        };
    }

    #endregion

    #region Size 序列化

    private Size ParseSize(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            // 格式: "Width,Height"
            var str = element.GetString() ?? "0,0";
            var parts = str.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height))
            {
                return new Size(width, height);
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            // 格式: { "Width": 1920, "Height": 1080 }
            var width = element.GetProperty("Width").GetInt32();
            var height = element.GetProperty("Height").GetInt32();
            return new Size(width, height);
        }

        return new Size(0, 0);
    }

    private Dictionary<string, int> SerializeSize(Size size)
    {
        return new Dictionary<string, int>
        {
            ["Width"] = size.Width,
            ["Height"] = size.Height
        };
    }

    #endregion

    #region 新增几何类型序列化 (Circle, Line, Annulus, RotatedRect, Polygon)

    private Circle2d ParseCircle(JsonElement element)
    {
        var center = element.GetProperty("Center");
        var cx = center.GetProperty("X").GetDouble();
        var cy = center.GetProperty("Y").GetDouble();
        var radius = element.GetProperty("Radius").GetDouble();
        return new Circle2d(cx, cy, radius);
    }

    private Dictionary<string, object> SerializeCircle(Circle2d circle)
    {
        return new Dictionary<string, object>
        {
            ["Center"] = new Dictionary<string, double> { ["X"] = circle.Center.X, ["Y"] = circle.Center.Y },
            ["Radius"] = circle.Radius
        };
    }

    private Line2d ParseLine(JsonElement element)
    {
        var ptEl = element.GetProperty("Point");
        var px = ptEl.GetProperty("X").GetDouble();
        var py = ptEl.GetProperty("Y").GetDouble();
        var dirEl = element.GetProperty("Direction");
        var dx = dirEl.GetProperty("X").GetDouble();
        var dy = dirEl.GetProperty("Y").GetDouble();
        return new Line2d(new System.Windows.Point(px, py), new System.Windows.Vector(dx, dy));
    }

    private Dictionary<string, object> SerializeLine(Line2d line)
    {
        return new Dictionary<string, object>
        {
            ["Point"] = new Dictionary<string, double> { ["X"] = line.Point.X, ["Y"] = line.Point.Y },
            ["Direction"] = new Dictionary<string, double> { ["X"] = line.Direction.X, ["Y"] = line.Direction.Y }
        };
    }

    private Annulus2d ParseAnnulus(JsonElement element)
    {
        var centerEl = element.GetProperty("Center");
        var cx = centerEl.GetProperty("X").GetDouble();
        var cy = centerEl.GetProperty("Y").GetDouble();
        var innerR = element.GetProperty("InnerR").GetDouble();
        var outerR = element.GetProperty("OuterR").GetDouble();
        var startA = element.GetProperty("StartA").GetDouble();
        var endA = element.GetProperty("EndA").GetDouble();
        return new Annulus2d(cx, cy, innerR, outerR, startA, endA);
    }

    private Dictionary<string, object> SerializeAnnulus(Annulus2d annulus)
    {
        return new Dictionary<string, object>
        {
            ["Center"] = new Dictionary<string, double> { ["X"] = annulus.Center.X, ["Y"] = annulus.Center.Y },
            ["InnerR"] = annulus.InnerRadius,
            ["OuterR"] = annulus.OuterRadius,
            ["StartA"] = annulus.StartAngle,
            ["EndA"] = annulus.EndAngle
        };
    }

    private Dictionary<string, double> ParseRotatedRect(JsonElement element)
    {
        return new Dictionary<string, double>
        {
            ["Cx"] = element.GetProperty("Cx").GetDouble(),
            ["Cy"] = element.GetProperty("Cy").GetDouble(),
            ["W"] = element.GetProperty("W").GetDouble(),
            ["H"] = element.GetProperty("H").GetDouble(),
            ["Angle"] = element.GetProperty("Angle").GetDouble()
        };
    }

        private Dictionary<string, double>[]? ParsePolygon(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Array)
                return null;

            var points = new List<Dictionary<string, double>>();
            foreach (var item in element.EnumerateArray())
            {
                points.Add(new Dictionary<string, double>
                {
                    ["X"] = item.GetProperty("X").GetDouble(),
                    ["Y"] = item.GetProperty("Y").GetDouble()
                });
            }
            return points.ToArray();
        }

    #endregion

    #region Image (Mat) 序列化

    private Mat? ParseImage(JsonElement element)
    {
        // 图像数据以Base64字符串存储
        if (element.ValueKind != JsonValueKind.String)
            return null;

        try
        {
            var base64 = element.GetString();
            if (string.IsNullOrEmpty(base64))
                return null;

            var bytes = Convert.FromBase64String(base64);
            return Mat.FromImageData(bytes, ImreadModes.Color);
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(VisionLogLevel.Error, 
                $"解析图像数据失败: {ex.Message}", 
                "GlobalVariableValueConverter", ex);
            return null;
        }
    }

    private string? SerializeImage(Mat mat)
    {
        if (mat == null || mat.Empty())
            return null;

        try
        {
            var bytes = mat.ToBytes(".png");
            return Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(VisionLogLevel.Error, 
                $"序列化图像数据失败: {ex.Message}", 
                "GlobalVariableValueConverter", ex);
            return null;
        }
    }

    #endregion
}
