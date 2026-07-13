using System;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 类型映射器 — 将 UI 类型名（"Image"、"Integer" 等）映射到 C# 类型名/默认值。
    /// 统一入口，消除 ScriptInputConfig/ScriptOutputConfig/ScriptBindingResolver 三处的 switch 重复。
    ///
    /// 规则：
    /// - 新增类型只需在此类中添加映射，无需修改其他文件
    /// - 未知类型直接抛 ArgumentOutOfRangeException（rule-008 报错优先）
    /// </summary>
    internal static class ScriptTypeMapper
    {
        /// <summary>
        /// 映射为 C# 类型名（供生成字段声明和自动补全）
        /// </summary>
        public static string ToCsTypeName(string typeName) => typeName switch
        {
            "Image"       => "IVisionImage",
            "Integer"     => "int",
            "Integer[]"   => "int[]",
            "Double"      => "double",
            "Double[]"    => "double[]",
            "Float"       => "double",
            "Float[]"     => "double[]",
            "String"      => "string",
            "String[]"    => "string[]",
            "Boolean"     => "bool",
            "Byte"        => "byte",
            "Point"       => "SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.Point2D",
            "Rectangle"   => "SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters",
            "Size"        => "System.Windows.Size",
            "Region"      => "IVisionRegion",
            "Circle"      => "SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters",
            "Line"        => "SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters",
            "Annulus"     => "SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters",
            "RotatedRect" => "SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters",
            "Polygon"     => "SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters",
            _ => throw new ArgumentOutOfRangeException(nameof(typeName),
                $"未知的类型名: '{typeName}'。请在 ScriptTypeMapper.ToCsTypeName 中添加映射。")
        };

        /// <summary>
        /// 获取字段声明的默认值表达式（含 "= value" 部分，供 GenerateFieldCode 拼接使用）
        /// </summary>
        public static string GetFieldDefault(string typeName) => typeName switch
        {
            "Image"       => "= null!",
            "String"      => "= string.Empty",
            "Integer"     => "= 0",
            "Integer[]"   => "= null!",
            "Double"      => "= 0.0",
            "Double[]"    => "= null!",
            "Float"       => "= 0.0",
            "Float[]"     => "= null!",
            "String[]"    => "= null!",
            "Boolean"     => "= false",
            "Byte"        => "= 0",
            "Point"       => "= new SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.Point2D()",
            "Rectangle"   => "= new SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters { ShapeType = SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeType.Rectangle }",
            "Size"        => "= default",
            "Region"      => "= null!",
            "Circle"      => "= new SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters { ShapeType = SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeType.Circle }",
            "Line"        => "= new SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters { ShapeType = SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeType.Line }",
            "Annulus"     => "= new SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters { ShapeType = SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeType.Annulus }",
            "RotatedRect" => "= new SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters { ShapeType = SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeType.RotatedRectangle }",
            "Polygon"     => "= new SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeParameters { ShapeType = SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeType.Polygon }",
            _ => throw new ArgumentOutOfRangeException(nameof(typeName),
                $"未知的类型名: '{typeName}'。请在 ScriptTypeMapper.GetFieldDefault 中添加映射。")
        };

        /// <summary>
        /// 获取 CLR 默认值（供 ScriptBindingResolver 绑定解析未配置时使用）
        /// </summary>
        public static object? GetDefaultValue(string typeName) => typeName switch
        {
            "Image"       => null,
            "String"      => string.Empty,
            "Integer"     => 0,
            "Integer[]"   => null,
            "Double"      => 0.0,
            "Double[]"    => null,
            "Float"       => 0.0,
            "Float[]"     => null,
            "String[]"    => null,
            "Boolean"     => false,
            "Byte"        => (byte)0,
            "Point"       => null,
            "Rectangle"   => null,
            "Size"        => null,
            "Region"      => null,
            "Circle"      => null,
            "Line"        => null,
            "Annulus"     => null,
            "RotatedRect" => null,
            "Polygon"     => null,
            _ => throw new ArgumentOutOfRangeException(nameof(typeName),
                $"未知的类型名: '{typeName}'。请在 ScriptTypeMapper.GetDefaultValue 中添加映射。")
        };
    }
}
