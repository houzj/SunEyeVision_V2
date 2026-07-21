using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 属性路径解析器 — 支持数组索引语法
    /// </summary>
    /// <remarks>
    /// 解析格式: "PropertyName" / "PropertyName[]" / "PropertyName[N]"
    /// 
    /// 语义:
    /// - "PropertyName"     → 普通属性，取值原样返回
    /// - "PropertyName[]"   → 数组整体，取值返回整个数组
    /// - "PropertyName[0]"  → 数组索引，取值返回第0个元素
    /// </remarks>
    public static class PropertyPathParser
    {
    /// <summary>
    /// 解析属性路径片段，提取属性名、数组索引与类型过滤
    /// </summary>
    /// <param name="part">路径片段，如 "HistogramData", "HistogramData[]", "HistogramData[0]", "RoI[Type=Circle]"</param>
    /// <returns>纯属性名、数组索引（null 表示无索引或空括号）、类型过滤 token（null 表示无类型过滤）</returns>
    public static (string propertyName, int? arrayIndex, string? typeFilter) ParsePropertyPart(string part)
    {
        if (string.IsNullOrEmpty(part))
            return (part, null, null);

        var bracketStart = part.IndexOf('[');
        if (bracketStart < 0)
            return (part, null, null);

        var bracketEnd = part.IndexOf(']', bracketStart);
        if (bracketEnd < 0)
            return (part, null, null);

        var propName = part.Substring(0, bracketStart);
        var indexStr = part.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

        // [] 空括号 → 使用整个数组
        if (string.IsNullOrEmpty(indexStr))
            return (propName, null, null);

        // [Type=XXX] → 按图形类型过滤（RoI 多图形统一绑定，方案步骤2）
        const string typeMarker = "Type=";
        if (indexStr.StartsWith(typeMarker, StringComparison.OrdinalIgnoreCase))
        {
            var typeToken = indexStr.Substring(typeMarker.Length);
            return (propName, null, typeToken);
        }

        // [0] [1] 等具体索引
        if (int.TryParse(indexStr, out var index))
            return (propName, index, null);

        // 无效内容 → 当作普通属性
        return (part, null, null);
    }

        /// <summary>
        /// 从属性路径片段中提取纯属性名（去掉索引部分）
        /// </summary>
        public static string ExtractPropertyName(string part)
        {
            return ParsePropertyPart(part).propertyName;
        }

        /// <summary>
        /// 判断属性路径片段是否包含数组索引标记（[] 或 [N]）
        /// </summary>
        public static bool HasArrayNotation(string part)
        {
            if (string.IsNullOrEmpty(part))
                return false;
            return part.Contains('[') && part.Contains(']');
        }

        /// <summary>
        /// 解析完整属性路径（支持点号嵌套 + 数组索引 + 类型过滤）
        /// </summary>
        /// <param name="propertyPath">如 "CircleFound.Radius" 或 "HistogramData[0]" 或 "RoI[Type=Circle]"</param>
        /// <returns>每段解析结果</returns>
        public static List<(string propertyName, int? arrayIndex, string? typeFilter)> ParsePropertyPath(string propertyPath)
        {
            var result = new List<(string, int?, string?)>();
            if (string.IsNullOrEmpty(propertyPath))
                return result;

            foreach (var part in propertyPath.Split('.'))
            {
                result.Add(ParsePropertyPart(part));
            }

            return result;
        }
    }
}
