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
    /// <param name="part">路径片段，如 "HistogramData", "HistogramData[]", "HistogramData[0]", "RoI[Type=Circle]", "RoI[Type=Circle][2]"</param>
    /// <returns>纯属性名、数组索引（null 表示无索引或空括号）、类型过滤 token（null 表示无类型过滤）</returns>
    /// <remarks>
    /// 支持"类型过滤 + 索引"组合（如 "[Type=Circle][2]"）：先按类型过滤出子集，再取第 n 个。
    /// 顺序固定为 [Type=X][n]（方案 C 组，P5）。
    /// </remarks>
    public static (string propertyName, int? arrayIndex, string? typeFilter) ParsePropertyPart(string part)
    {
        if (string.IsNullOrEmpty(part))
            return (part, null, null);

        var bracketStart = part.IndexOf('[');
        if (bracketStart < 0)
            return (part, null, null);

        var propName = part.Substring(0, bracketStart);
        var rest = part.Substring(bracketStart);
        int? arrayIndex = null;
        string? typeFilter = null;

        // 逐对解析括号：首对可为 [Type=X] 或 [n]/[]；后续仅允许 [n]/[]
        var cursor = 0;
        while (cursor < rest.Length)
        {
            var bs = rest.IndexOf('[', cursor);
            if (bs < 0)
                break;
            var be = rest.IndexOf(']', bs);
            if (be < 0)
                break;

            var token = rest.Substring(bs + 1, be - bs - 1);

            if (typeFilter == null && token.StartsWith("Type=", StringComparison.OrdinalIgnoreCase))
            {
                // 首对 [Type=XXX] → 类型过滤
                typeFilter = token.Substring("Type=".Length);
            }
            else if (token.Length == 0)
            {
                // [] 空括号 → 显式取整个数组（哨兵 -1）
                arrayIndex = -1;
            }
            else if (int.TryParse(token, out var idx))
            {
                // [n] 具体索引
                arrayIndex = idx;
            }
            else
            {
                // 非法 token（非 Type=、非空、非数字）→ 视为无效路径，整段回退为普通属性
                return (part, null, null);
            }

            cursor = be + 1;
        }

        return (propName, arrayIndex, typeFilter);
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
