using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using VisionLogger = SunEyeVision.Plugin.SDK.Logging.VisionLogger;
using VisionLogLevel = SunEyeVision.Plugin.SDK.Logging.LogLevel;

namespace SunEyeVision.Workflow;

/// <summary>
/// 全局变量值转换器 - 保留类型信息的序列化/反序列化。
///
/// 类型词汇由 SdkTypeVocabulary 唯一权威定义（SSOT），
/// 本类不定义任何类型常量，所有类型名通过 SdkTypeVocabulary 解析。
///
/// 序列化格式：{ "Type": "Double", "Value": [3.14] }
/// 支持的类型（7 个核心概念）：整型(Integer)/双精度(Double)/字符串(String)/布尔(Boolean)/
/// 图像(Image)/图形定义(RoI)/变换矩阵(Transform)。
/// 区域(Region)为算法内部 iconic 数据，禁止序列化，由 RegionConverter 桥接为 RoI 后外发（方案 D8）。
/// 所有基础类型强制以 T[] 形态存储（填 3.14 → 落盘为 [3.14]，对齐 tuple 语义，方案步骤7）。
/// 历史标量形态 JSON 重开时由 ParseAdaptive 抛异常，需重新保存升级为 T[]。
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

        // 基础类型：强制 T[]（方案步骤7）。ParseAdaptive 仅接受数组形态，
        // 历史标量 JSON 会抛异常（数据需重新保存升级），新写入由 SerializeValue 统一包成数组。
        if (clrType == typeof(long) || clrType == typeof(long[]))
            return ParseAdaptive(valueElement, e => e.GetInt64());
        if (clrType == typeof(string) || clrType == typeof(string[]))
            return ParseAdaptive(valueElement, e => e.GetString()!);
        if (clrType == typeof(bool) || clrType == typeof(bool[]))
            return ParseAdaptive(valueElement, e => e.GetBoolean());
        if (clrType == typeof(double) || clrType == typeof(double[]))
            return ParseAdaptive(valueElement, e => e.GetDouble());
        // 变换矩阵：保持对象形态（非数组），与基础类型的 T[] 强制互不干扰（已知限制：类型对称未统一）。
        if (clrType == typeof(AffineMatrix))
            return ParseAffineMatrix(valueElement);
        if (clrType == typeof(AffineMatrix[]))
            return ParseAdaptive(valueElement, ParseAffineMatrix);
        if (clrType == typeof(IVisionImage))
            return ParseImage(valueElement);

        // RoI（图形定义）：数组形态承载，几何多态由 GeometryJsonConverter 处理
        if (clrType == typeof(ShapeParameters[]))
            return ParseShapeParameters(valueElement, options);

        throw new NotSupportedException($"不支持的类型: '{typeName}' (CLR: {clrType.FullName})");
    }

    /// <summary>
    /// 值形态解析：数组 → T[]。
    /// 强制 T[] 契约（方案步骤7 + rule-015 报错优先）：标量 JSON 不再被静默提升为单值，
    /// 历史以标量形态存储的全局变量（重开旧方案）会直接抛异常，需重新保存升级为 T[] 形态。
    /// 新写入的值由 <see cref="SerializeValue"/> 统一以数组形态落盘（如 3.14 → [3.14]）。
    /// </summary>
    private static object? ParseAdaptive<T>(JsonElement element, Func<JsonElement, T> selector)
    {
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Select(selector).ToArray();

        throw new NotSupportedException(
            $"全局变量值必须是数组形态 (T[])，但收到标量值。请重新保存该变量以升级为 T[] 存储（对齐 tuple 语义）。");
    }

    /// <summary>
    /// 解析 RoI（图形定义）值：数组 → ShapeParameters[]，单值 → 长度 1 数组（tuple 语义）。
    /// 几何多态由 <see cref="GeometryJsonConverter"/> 在 ShapeParameters.Geometry 属性上处理。
    /// </summary>
    private static ShapeParameters[]? ParseShapeParameters(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind == JsonValueKind.Null)
            return null;
        if (element.ValueKind == JsonValueKind.Array)
            return JsonSerializer.Deserialize<ShapeParameters[]>(element.GetRawText(), options);
        var single = JsonSerializer.Deserialize<ShapeParameters>(element.GetRawText(), options);
        return single == null ? null : new[] { single };
    }

    private (string TypeName, object? SerializedValue) SerializeValue(object value, JsonSerializerOptions options)
    {
        var typeName = SdkTypeVocabulary.ToTypeVocabulary(value.GetType());
        if (typeName == null)
            throw new NotSupportedException(
                $"类型 '{value.GetType().FullName}' 未在 SdkTypeVocabulary 中注册");

        return value switch
        {
            // 强制 T[]（方案步骤7）：标量统一包成长度 1 的数组（填 3.14 → 落盘为 [3.14]），
            // 与词汇表 tuple 语义一致；历史标量 JSON 重开由 ParseAdaptive 抛异常（需重新保存）。
            // 整型：int/long 统一序列化为 "Integer"（int 提升为 long，避免像素/面积溢出）
            int intValue    => (typeName, new[] { (long)intValue }),
            long longValue  => (typeName, new[] { longValue }),
            int[] intArr    => (typeName, intArr.Select(Convert.ToInt64).ToArray()),
            long[] longArr  => (typeName, longArr),
            // 双精度：float/double 统一序列化为 "Double"
            float fltValue  => (typeName, new[] { (double)fltValue }),
            double dblValue => (typeName, new[] { dblValue }),
            float[] fltArr  => (typeName, fltArr.Select(Convert.ToDouble).ToArray()),
            double[] dblArr => (typeName, dblArr),
            string strValue => (typeName, new[] { strValue }),
            string[] strArr => (typeName, strArr),
            bool boolValue  => (typeName, new[] { boolValue }),
            bool[] boolArr  => (typeName, boolArr),
            ShapeParameters sp  => (typeName, new[] { sp }),
            ShapeParameters[] sp => (typeName, sp),
            Mat mat         => (typeName, SerializeImage(mat)),
            AffineMatrix m  => (typeName, SerializeAffineMatrix(m)),
            AffineMatrix[] ms => (typeName, ms.Select(SerializeAffineMatrix).ToArray()),
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
