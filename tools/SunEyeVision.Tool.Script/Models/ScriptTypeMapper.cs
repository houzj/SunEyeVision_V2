using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 类型映射器 — 将 UI 类型名（"Image"、"Integer" 等）映射到 C# 类型名/默认值。
    ///
    /// v2 设计：退化为 SdkTypeVocabulary 委派。
    /// - SDK 的 TypeToString 是类型词汇唯一权威（决策2）
    /// - 本类的独有职责：SDK CLR Type → 脚本层偏好的 C# 实现类型（如几何→ShapeParameters）
    /// - 几何类型字段自动数组化（决策1：绑定默认 T[]）
    /// - 未知类型直接抛 ArgumentOutOfRangeException（rule-008 / rule-015）
    /// </summary>
    internal static class ScriptTypeMapper
    {
        /// <summary>
        /// SDK CLR Type → 脚本层偏好的 C# 实现类型。
        /// 绝大部分派生自 SDK 的 Type.FullName，仅几何/Region 类型需特化。
        /// </summary>
        private static readonly Dictionary<Type, string> SdkTypeToCsName = new()
        {
            [typeof(int)]    = "int",
            [typeof(string)] = "string",
            [typeof(bool)]   = "bool",
            [typeof(byte)]   = "byte",
            [typeof(double)] = "double",
            [typeof(float)]  = "double",           // 脚本层用 double 兼容 float
            [typeof(System.Windows.Point)] = "System.Windows.Point",
            [typeof(System.Windows.Size)]  = "System.Windows.Size",
            [typeof(SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.Point2D)]
                = "SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.Point2D",
            [typeof(SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters)]
                = "SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters",
            // 几何 readonly struct → 脚本需可写的 ShapeParameters（可设 ShapeType）
            [typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Circle2d)]
                = "SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters",
            [typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Line2d)]
                = "SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters",
            [typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Annulus2d)]
                = "SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters",
            [typeof(SunEyeVision.Plugin.SDK.Models.Geometry.RotatedRect2d)]
                = "SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters",
            [typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Polygon2d)]
                = "SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters",
            [typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Arc2d)]
                = "SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters",
            [typeof(IVisionImage)]
                = "IVisionImage",
        };

        /// <summary>
        /// 几何类型集合：这些类型在脚本中自动数组化（决策1）。
        /// </summary>
        private static readonly HashSet<Type> GeometryClrTypes = new()
        {
            typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Circle2d),
            typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Line2d),
            typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Annulus2d),
            typeof(SunEyeVision.Plugin.SDK.Models.Geometry.RotatedRect2d),
            typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Polygon2d),
            typeof(SunEyeVision.Plugin.SDK.Models.Geometry.Arc2d),
            typeof(SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters),
            typeof(SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.Point2D),
            typeof(System.Windows.Point),
            typeof(System.Windows.Size),
        };

        /// <summary>
        /// 几何类型 → ShapeType 默认值（字段声明用）。
        /// </summary>
        private static readonly Dictionary<string, string> GeometryFieldDefaults = new()
        {
            ["Point"]       = "= new SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.Point2D()",
            ["Rectangle"]   = "= new SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters { Geometry = new SunEyeVision.Plugin.SDK.Models.Geometry.RotatedRect2d(0,0,100,100,0) }",
            ["Circle"]      = "= new SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters { Geometry = new SunEyeVision.Plugin.SDK.Models.Geometry.Circle2d(0,0,50) }",
            ["Line"]        = "= new SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters { Geometry = SunEyeVision.Plugin.SDK.Models.Geometry.Line2d.FromTwoPoints(new System.Windows.Point(0,0), new System.Windows.Point(100,100)) }",
            ["Annulus"]     = "= new SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters { Geometry = new SunEyeVision.Plugin.SDK.Models.Geometry.Annulus2d(0,0,30,80) }",
            ["RotatedRect"] = "= new SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters { Geometry = new SunEyeVision.Plugin.SDK.Models.Geometry.RotatedRect2d(0,0,100,100,0) }",
            ["Polygon"]     = "= new SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeParameters { Geometry = new SunEyeVision.Plugin.SDK.Models.Geometry.Polygon2d(new System.Windows.Point[] { new System.Windows.Point(0,0), new System.Windows.Point(100,0), new System.Windows.Point(50,100) }) }",
        };

        private static Type GetClrTypeOrThrow(string typeName)
        {
            var clrType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToSystemType(typeName);
            if (clrType == null)
                throw new ArgumentOutOfRangeException(nameof(typeName),
                    $"未知的类型名: '{typeName}'。请在 SdkTypeVocabulary 中添加映射。");
            return clrType;
        }

        /// <summary>
        /// 映射为 C# 类型名（供生成字段声明和自动补全）。
        /// 基础值类型返回标量（int/double/bool/byte）；
        /// 几何类型自动数组化（ShapeParameters[]），实现决策1"默认 T[]"。
        /// </summary>
        public static string ToCsTypeName(string typeName)
        {
            var baseName = typeName.EndsWith("[]") ? typeName[..^2] : typeName;
            var clrType = GetClrTypeOrThrow(baseName);

            if (!SdkTypeToCsName.TryGetValue(clrType, out var csName))
                csName = clrType.FullName!;

            // 几何类型以及显式数组词目 → 自动加 []
            if (typeName.EndsWith("[]") || GeometryClrTypes.Contains(clrType))
                return csName + "[]";

            return csName;
        }

        /// <summary>
        /// 获取字段声明的默认值表达式（含 "= value" 部分，供 GenerateFieldCode 拼接使用）。
        /// 几何类型有 ShapeType 初始化表达式；基础类型自动推导。
        /// </summary>
        public static string GetFieldDefault(string typeName)
        {
            // 几何单值 → ShapeType 初始化表达式（含 new ...）
            if (!typeName.EndsWith("[]") && GeometryFieldDefaults.TryGetValue(typeName, out var geoDefault))
                return geoDefault;

            var clrType = GetClrTypeOrThrow(typeName);

            if (clrType == typeof(string))
                return "= string.Empty";
            if (typeName.EndsWith("[]") || !clrType.IsValueType)
                return "= null!";

            // 值类型
            if (clrType == typeof(int))    return "= 0";
            if (clrType == typeof(double)) return "= 0.0";
            if (clrType == typeof(bool))   return "= false";
            if (clrType == typeof(byte))   return "= 0";
            if (clrType == typeof(float))  return "= 0.0";

            return "= default";
        }

        /// <summary>
        /// 获取 CLR 默认值（供 ScriptBindingResolver 绑定解析未配置时使用）。
        /// </summary>
        public static object? GetDefaultValue(string typeName)
        {
            // 几何单值 → null（复杂类型运行时不可靠）
            if (!typeName.EndsWith("[]") && GeometryFieldDefaults.ContainsKey(typeName))
                return null;

            var clrType = GetClrTypeOrThrow(typeName);

            if (clrType == typeof(string))
                return string.Empty;
            if (typeName.EndsWith("[]") || !clrType.IsValueType)
                return null;

            if (clrType == typeof(int))    return 0;
            if (clrType == typeof(double)) return 0.0;
            if (clrType == typeof(bool))   return false;
            if (clrType == typeof(byte))   return (byte)0;
            if (clrType == typeof(float))  return 0.0;

            return Activator.CreateInstance(clrType);
        }
    }
}
