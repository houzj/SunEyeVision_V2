using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using VisionLogger = SunEyeVision.Plugin.SDK.Logging.VisionLogger;
using VisionLogLevel = SunEyeVision.Plugin.SDK.Logging.LogLevel;

namespace SunEyeVision.Workflow;

/// <summary>
/// 全局变量值转换器 - 保留类型信息的序列化/反序列化。
///
/// 类型词汇由 SdkTypeVocabulary 唯一权威定义（SSOT），
/// 本类不定义任何类型常量，所有类型名通过 SdkTypeVocabulary 解析。
///
/// 序列化格式：{ "Type": "Double", "Value": 3.14 }
/// 支持的类型（7 个核心概念）：整型(Integer)/双精度(Double)/字符串(String)/布尔(Boolean)/
/// 图像(Image)/区域(Region)/变换矩阵(Transform)。
/// 历史存量类型名（Xxx[]、Float、Rectangle）可加载，Byte 及几何类型已删除并 fail-fast（rule-015）。
/// </summary>
public class GlobalVariableValueConverter : JsonConverter<object?>
{
    #region 反序列化

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

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

    #endregion

    #region 序列化

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

    #endregion

    #region 调度 — 通过 SdkTypeVocabulary 唯一权威解析类型名

    /// <summary>
    /// 解析值。类型名通过 SdkTypeVocabulary 映射到 CLR 类型，然后分发。
    /// 新增类型时只需：1. 在 SdkTypeVocabulary 注册；2. 在本类加 ParseXxx/SerializeXxx 方法。
    /// </summary>
    private object? ParseValue(string typeName, JsonElement valueElement, JsonSerializerOptions options)
    {
        var clrType = SdkTypeVocabulary.ToSystemType(typeName);
        if (clrType == null)
            throw new NotSupportedException($"不支持的全局变量类型: '{typeName}'。请在 SdkTypeVocabulary 中添加映射。");

        // 基础类型：值形态自适应（标量 = T，数组 = T[]），同一类型名兼容单值与数组。
        if (clrType == typeof(long) || clrType == typeof(long[]))
            return ParseAdaptive(valueElement, e => e.GetInt64());
        if (clrType == typeof(string) || clrType == typeof(string[]))
            return ParseAdaptive(valueElement, e => e.GetString()!);
        if (clrType == typeof(bool) || clrType == typeof(bool[]))
            return ParseAdaptive(valueElement, e => e.GetBoolean());
        if (clrType == typeof(double) || clrType == typeof(double[]))
            return ParseAdaptive(valueElement, e => e.GetDouble());
        if (clrType == typeof(AffineMatrix) || clrType == typeof(AffineMatrix[]))
            return ParseAdaptive(valueElement, ParseAffineMatrix);
        if (clrType == typeof(IVisionImage))
            return ParseImage(valueElement);
        if (clrType == typeof(IVisionRegion))
            throw new NotSupportedException("区域类型不能序列化为JSON");

        throw new NotSupportedException($"不支持的类型: '{typeName}' (CLR: {clrType.FullName})");
    }

    /// <summary>
    /// 值形态自适应解析：数组 → T[]，标量 → T。
    /// 同一个类型名既兼容单值也兼容数组（对齐 Halcon tuple 语义，单值=长度1数组）。
    /// </summary>
    private static object? ParseAdaptive<T>(JsonElement element, Func<JsonElement, T> selector)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Select(selector).ToArray();
        return (object?)selector(element);
    }

    private (string TypeName, object? SerializedValue) SerializeValue(object value, JsonSerializerOptions options)
    {
        var typeName = SdkTypeVocabulary.ToTypeVocabulary(value.GetType());
        if (typeName == null)
            throw new NotSupportedException(
                $"类型 '{value.GetType().FullName}' 未在 SdkTypeVocabulary 中注册");

        return value switch
        {
            // 整型：int/long 统一序列化为 "Integer"（int 提升为 long，避免像素/面积溢出）
            int intValue    => (typeName, (long)intValue),
            long longValue  => (typeName, longValue),
            int[] intArr    => (typeName, intArr.Select(Convert.ToInt64).ToArray()),
            long[] longArr  => (typeName, longArr),
            // 双精度：float/double 统一序列化为 "Double"
            float fltValue  => (typeName, (double)fltValue),
            double dblValue => (typeName, dblValue),
            float[] fltArr  => (typeName, fltArr.Select(Convert.ToDouble).ToArray()),
            double[] dblArr => (typeName, dblArr),
            string strValue => (typeName, strValue),
            string[] strArr => (typeName, strArr),
            bool boolValue  => (typeName, boolValue),
            bool[] boolArr  => (typeName, boolArr),
            Mat mat         => (typeName, SerializeImage(mat)),
            AffineMatrix m  => (typeName, SerializeAffineMatrix(m)),
            AffineMatrix[] ms => (typeName, ms.Select(SerializeAffineMatrix).ToArray()),
            IVisionRegion   => throw new NotSupportedException("区域类型不能序列化为JSON"),
            _ => throw new NotSupportedException(
                $"类型 '{value.GetType().FullName}' 不是已注册的全局变量类型，请在 SdkTypeVocabulary 中添加映射")
        };
    }

    #endregion

    #region 变换矩阵 (AffineMatrix) 序列化

    private static AffineMatrix ParseAffineMatrix(JsonElement element)
    {
        return new AffineMatrix(
            element.GetProperty("M11").GetDouble(),
            element.GetProperty("M12").GetDouble(),
            element.GetProperty("M21").GetDouble(),
            element.GetProperty("M22").GetDouble(),
            element.GetProperty("OffsetX").GetDouble(),
            element.GetProperty("OffsetY").GetDouble());
    }

    private static Dictionary<string, double> SerializeAffineMatrix(AffineMatrix m) => new()
    {
        ["M11"] = m.M11, ["M12"] = m.M12, ["M21"] = m.M21,
        ["M22"] = m.M22, ["OffsetX"] = m.OffsetX, ["OffsetY"] = m.OffsetY
    };

    #endregion

    #region Image (Mat) 序列化

    private static Mat? ParseImage(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String) return null;
        try
        {
            var base64 = element.GetString();
            if (string.IsNullOrEmpty(base64)) return null;
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

    private static string? SerializeImage(Mat mat)
    {
        if (mat == null || mat.Empty()) return null;
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
