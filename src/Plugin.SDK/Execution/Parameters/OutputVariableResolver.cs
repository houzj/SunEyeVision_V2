using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 输出变量单一解析器（SSOT）。
    /// 取代 ParameterResolver/ScriptBindingResolver/AlgorithmNode/DataSourceQueryService 四处独立反射 + GetResultItems 兜底。
    /// 按 Name 从 OutputVariable 契约寻址，找不到即报错（rule-015）。
    /// </summary>
    public static class OutputVariableResolver
    {
        /// <summary>
        /// 从节点结果集中解析绑定值
        /// </summary>
        /// <param name="nodeResults">全量节点结果字典</param>
        /// <param name="nodeId">源节点 ID</param>
        /// <param name="propertyPath">属性路径，如 "ResultImage" / "CircleFound.Radius" / "Results[0].X"</param>
        /// <returns>解析到的值，未命中抛异常</returns>
        public static object? Resolve(
            IDictionary<string, ToolResults> nodeResults,
            string nodeId,
            string propertyPath)
        {
            if (!nodeResults.TryGetValue(nodeId, out var result) || result is null)
                throw new InvalidOperationException(
                    $"绑定源节点 '{nodeId}' 不在执行结果中。可用节点: [{string.Join(", ", nodeResults.Keys)}]");

            return Resolve(result, propertyPath, nodeId);
        }

        /// <summary>
        /// 从单个节点结果中解析属性值
        /// </summary>
        /// <param name="result">节点运行结果</param>
        /// <param name="propertyPath">属性路径</param>
        /// <param name="nodeId">节点 ID（仅用于错误信息）</param>
        /// <returns>解析到的值</returns>
        public static object? Resolve(ToolResults result, string propertyPath, string? nodeId = null)
        {
            var parsedPath = PropertyPathParser.ParsePropertyPath(propertyPath);
            if (parsedPath.Count == 0)
                throw new InvalidOperationException($"属性路径为空: '{propertyPath}'");

            var (firstName, firstIndex, firstTypeFilter) = parsedPath[0];
            var variables = result.GetOutputVariables();
            var variable = variables.FirstOrDefault(v =>
                string.Equals(v.Name, firstName, StringComparison.OrdinalIgnoreCase));

            if (variable == null)
            {
                var nodeLabel = nodeId != null ? $"节点 '{nodeId}'" : "结果";
                var available = variables.Count > 0
                    ? string.Join(", ", variables.Select(v => v.Name))
                    : "(空)";
                throw new InvalidOperationException(
                    $"{nodeLabel} 无输出变量 '{firstName}'（契约中不存在）。可用变量: [{available}]");
            }

            object? current = variable.RuntimeValue;

            // 第一段类型过滤（RoI 按图形类型绑定，方案步骤3）
            if (firstTypeFilter != null)
            {
                current = FilterByShapeType(current, firstName, firstTypeFilter);
            }

            // 第一段数组索引处理
            if (firstIndex.HasValue)
            {
                // firstIndex == -1 表示显式 "[]"，保留整个数组，不做任何事
                if (firstIndex.Value >= 0)
                    current = GetArrayElement(current, firstName, firstIndex.Value);
            }
            else if (firstTypeFilter == null)
            {
                // 无索引且非 [Type=] 过滤 → 长度 1 数组默认取 [0]（存量标量绑定兼容，决策3）
                current = UnwrapLengthOne(current);
            }

            // 后续段：在当前运行时值上用反射逐段解析
            for (int i = 1; i < parsedPath.Count; i++)
            {
                if (current == null)
                    throw new InvalidOperationException(
                        $"属性路径 '{propertyPath}' 中途遇到 null 值（取到 '{parsedPath[i].propertyName}' 时）");

                var (propName, idx, typeFilter) = parsedPath[i];
                var prop = current.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanRead)
                {
                    var actualType = current.GetType().Name;
                    throw new InvalidOperationException(
                        $"属性路径 '{propertyPath}' 中找不到属性 '{propName}'（运行时值类型 '{actualType}' 无此公共实例属性）");
                }

                try
                {
                    current = prop.GetValue(current);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"属性路径 '{propertyPath}' 获取 '{propName}' 失败: {ex.Message}", ex);
                }

                if (typeFilter != null)
                {
                    current = FilterByShapeType(current, propName, typeFilter);
                }

                if (idx.HasValue)
                {
                    // idx == -1 表示显式 "[]"，保留整个数组
                    if (idx.Value >= 0)
                        current = GetArrayElement(current, propName, idx.Value);
                }
                else if (typeFilter == null)
                {
                    current = UnwrapLengthOne(current);
                }
            }

            return current;
        }

        private static object? UnwrapLengthOne(object? current)
            => current is Array arr && arr.Length == 1 ? arr.GetValue(0) : current;

        private static object? GetArrayElement(object? current, string name, int index)
        {
            if (current is Array arr)
            {
                if (index < 0 || index >= arr.Length)
                    throw new IndexOutOfRangeException(
                        $"属性 '{name}' 的数组索引 {index} 越界（长度={arr.Length}）");
                return arr.GetValue(index);
            }
            throw new InvalidOperationException(
                $"属性 '{name}' 不是数组类型，但指定了索引 [{index}]（实际类型: {current?.GetType().Name ?? "null"}）");
        }

        private static object? FilterByShapeType(object? current, string name, string typeToken)
        {
            if (current is not IEnumerable<ShapeParameters> roi)
                throw new InvalidOperationException(
                    $"属性 '{name}' 指定了 [Type={typeToken}] 类型过滤，但运行时值不是 ShapeParameters[]（实际类型: {current?.GetType().Name ?? "null"}）");

            if (!Enum.TryParse<ShapeType>(typeToken, out var targetType))
                throw new InvalidOperationException(
                    $"属性 '{name}' 的 [Type={typeToken}] 不是有效的图形类型（可用: Circle/Rectangle/RotatedRectangle/Polygon/Annulus/Arc）");

            var filtered = roi.Where(sp => sp.ShapeType == targetType).ToArray();
            if (filtered.Length == 0)
                throw new InvalidOperationException(
                    $"属性 '{name}' 按 [Type={typeToken}] 过滤后无匹配元素（数组共 {roi.Count()} 项）");

            return filtered;
        }
    }
}
