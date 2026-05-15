using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HalconDotNet;
using Rect = System.Drawing.Rectangle;
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
/// - 支持14种基础类型和OpenCvSharp特殊类型
/// 
/// 支持的数据类型：
/// 1. 基础类型：Integer, Float, String, Boolean, Int64, Double, Byte
/// 2. 几何类型：Point, Rectangle, Size
/// 3. 集合类型：Array, List
/// 4. 图像类型：Image（Mat序列化）
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
        public const string Float = "Float";
        public const string String = "String";
        public const string Boolean = "Boolean";
        public const string Int64 = "Int64";
        public const string Double = "Double";
        public const string Byte = "Byte";
        public const string Point = "Point";
        public const string Rectangle = "Rectangle";
        public const string Size = "Size";
        public const string Array = "Array";
        public const string List = "List";
        public const string Image = "Image";
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
            SupportedTypes.Float => valueElement.GetSingle(),
            SupportedTypes.String => valueElement.GetString(),
            SupportedTypes.Boolean => valueElement.GetBoolean(),
            SupportedTypes.Int64 => valueElement.GetInt64(),
            SupportedTypes.Double => valueElement.GetDouble(),
            SupportedTypes.Byte => valueElement.GetByte(),
            SupportedTypes.Point => ParsePoint(valueElement),
            SupportedTypes.Rectangle => ParseRectangle(valueElement),
            SupportedTypes.Size => ParseSize(valueElement),
            SupportedTypes.Array => ParseArray(valueElement, options),
            SupportedTypes.List => ParseList(valueElement, options),
            SupportedTypes.Image => ParseImage(valueElement),
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
            float floatValue => (SupportedTypes.Float, floatValue),
            string stringValue => (SupportedTypes.String, stringValue),
            bool boolValue => (SupportedTypes.Boolean, boolValue),
            long longValue => (SupportedTypes.Int64, longValue),
            double doubleValue => (SupportedTypes.Double, doubleValue),
            byte byteValue => (SupportedTypes.Byte, byteValue),
            Point point => (SupportedTypes.Point, SerializePoint(point)),
            Rect rect => (SupportedTypes.Rectangle, SerializeRectangle(rect)),
            Size size => (SupportedTypes.Size, SerializeSize(size)),
            Array array => (SupportedTypes.Array, SerializeArray(array)),
            IList list => (SupportedTypes.List, SerializeList(list)),
            HImage hImage => (SupportedTypes.Image, SerializeImage(hImage)),
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

    #region Array 序列化

    private Array? ParseArray(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(ParseArrayItem(item, options));
        }

        return list.ToArray();
    }

    private object? ParseArrayItem(JsonElement element, JsonSerializerOptions options)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private List<object?> SerializeArray(Array array)
    {
        var list = new List<object?>();
        foreach (var item in array)
        {
            list.Add(item);
        }
        return list;
    }

    #endregion

    #region List 序列化

    private IList? ParseList(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(ParseArrayItem(item, options));
        }

        return list;
    }

    private List<object?> SerializeList(IList list)
    {
        var result = new List<object?>();
        foreach (var item in list)
        {
            result.Add(item);
        }
        return result;
    }

    #endregion

    #region Image (HImage) 序列化

    private HImage? ParseImage(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
            return null;

        try
        {
            var base64 = element.GetString();
            if (string.IsNullOrEmpty(base64))
                return null;

            var bytes = Convert.FromBase64String(base64);
            string tempPath = Path.GetTempFileName() + ".png";
            try
            {
                File.WriteAllBytes(tempPath, bytes);
                HOperatorSet.ReadImage(out var image, tempPath);
                return new HImage(image);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(VisionLogLevel.Error,
                $"解析图像数据失败: {ex.Message}",
                "GlobalVariableValueConverter", ex);
            return null;
        }
    }

    private string? SerializeImage(HImage image)
    {
        if (image == null || !image.IsInitialized())
            return null;

        try
        {
            string tempPath = Path.GetTempFileName() + ".png";
            try
            {
                image.WriteImage("png", 0, tempPath);
                var bytes = File.ReadAllBytes(tempPath);
                return Convert.ToBase64String(bytes);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
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
