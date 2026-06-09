using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Workflow;

/// <summary>
/// 全局变量类型映射器 — 15 种类型对称映射，System.Type ↔ GlobalVariable 类型字符串
/// </summary>
/// <remarks>
/// 只包含通用 .NET 类型、OpenCvSharp 类型和 SDK 接口类型。
/// 不包含 Halcon 等具体视觉算法库类型。
/// 未映射的类型（如 Halcon HImage、HRegion）→ 选择器显示全部。
/// </remarks>
public static class GlobalVariableTypeMapper
{
    /// <summary>
    /// System.Type → GlobalVariable 类型字符串
    /// </summary>
    private static readonly Dictionary<Type, string> TypeToString = new()
    {
        // 基础类型（5 个）
        [typeof(int)]    = GlobalVariableValueConverter.SupportedTypes.Integer,
        [typeof(string)] = GlobalVariableValueConverter.SupportedTypes.String,
        [typeof(bool)]   = GlobalVariableValueConverter.SupportedTypes.Boolean,
        [typeof(double)] = GlobalVariableValueConverter.SupportedTypes.Double,
        [typeof(byte)]   = GlobalVariableValueConverter.SupportedTypes.Byte,

        // 几何类型（3 个）— 支持 WPF 和 OpenCvSharp 两种类型映射到同一字符串
        [typeof(System.Windows.Point)] = GlobalVariableValueConverter.SupportedTypes.Point,
        [typeof(OpenCvSharp.Point)]    = GlobalVariableValueConverter.SupportedTypes.Point,
        [typeof(System.Windows.Rect)]  = GlobalVariableValueConverter.SupportedTypes.Rectangle,
        [typeof(OpenCvSharp.Rect)]     = GlobalVariableValueConverter.SupportedTypes.Rectangle,
        [typeof(System.Windows.Size)]  = GlobalVariableValueConverter.SupportedTypes.Size,
        [typeof(OpenCvSharp.Size)]     = GlobalVariableValueConverter.SupportedTypes.Size,

        // 图像类型
        [typeof(IVisionImage)] = GlobalVariableValueConverter.SupportedTypes.Image,

        // SDK 几何模型类型
        [typeof(IVisionRegion)] = GlobalVariableValueConverter.SupportedTypes.Region,
        [typeof(Circle2d)]      = GlobalVariableValueConverter.SupportedTypes.Circle,
        [typeof(Line2d)]        = GlobalVariableValueConverter.SupportedTypes.Line,
        [typeof(Annulus2d)]     = GlobalVariableValueConverter.SupportedTypes.Annulus,
    };

    /// <summary>
    /// GlobalVariable 类型字符串 → System.Type（取第一个映射的 Type）
    /// </summary>
    private static readonly Dictionary<string, Type> StringToType =
        TypeToString.GroupBy(kvp => kvp.Value)
            .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 中文显示名（15 种类型全对称）
    /// </summary>
    public static readonly Dictionary<string, string> ChineseNames = new()
    {
        [GlobalVariableValueConverter.SupportedTypes.Integer]    = "整数",
        [GlobalVariableValueConverter.SupportedTypes.String]     = "字符串",
        [GlobalVariableValueConverter.SupportedTypes.Boolean]    = "布尔",
        [GlobalVariableValueConverter.SupportedTypes.Double]     = "双精度",
        [GlobalVariableValueConverter.SupportedTypes.Byte]       = "字节",
        [GlobalVariableValueConverter.SupportedTypes.Point]      = "点",
        [GlobalVariableValueConverter.SupportedTypes.Rectangle]  = "矩形",
        [GlobalVariableValueConverter.SupportedTypes.Size]       = "尺寸",
        [GlobalVariableValueConverter.SupportedTypes.Image]      = "图像",
        [GlobalVariableValueConverter.SupportedTypes.Region]     = "区域",
        [GlobalVariableValueConverter.SupportedTypes.Circle]     = "圆",
        [GlobalVariableValueConverter.SupportedTypes.Line]       = "直线",
        [GlobalVariableValueConverter.SupportedTypes.RotatedRect] = "旋转矩形",
        [GlobalVariableValueConverter.SupportedTypes.Polygon]    = "多边形",
        [GlobalVariableValueConverter.SupportedTypes.Annulus]    = "圆环",
    };

    /// <summary>
    /// System.Type 映射为 GlobalVariable 类型字符串
    /// </summary>
    public static string? ToGlobalVarType(Type? type)
        => type != null && TypeToString.TryGetValue(type, out var result) ? result : null;

    /// <summary>
    /// GlobalVariable 类型字符串映射为 System.Type
    /// </summary>
    public static Type? ToSystemType(string? globalVarType)
        => globalVarType != null && StringToType.TryGetValue(globalVarType, out var result) ? result : null;

    /// <summary>
    /// 获取中文显示名
    /// </summary>
    public static string? GetChineseName(string? typeName)
        => typeName != null && ChineseNames.TryGetValue(typeName, out var name) ? name : typeName;

    /// <summary>
    /// 获取所有类型选项（用于 ComboBox 绑定）
    /// </summary>
    public static List<TypeOption> GetAllTypeOptions()
        => ChineseNames.Select(kvp => new TypeOption(kvp.Value, kvp.Key)).ToList();
}

/// <summary>
/// 类型选项（ComboBox 绑定用，中文显示名 + 英文值）
/// </summary>
public class TypeOption
{
    public string DisplayName { get; }
    public string Value { get; }

    public TypeOption(string displayName, string value)
    {
        DisplayName = displayName;
        Value = value;
    }
}
