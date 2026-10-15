using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;

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
                var renamedHint = variables.Count > 0
                    ? " 该输出可能已改名或删除，请到源节点重新绑定。"
                    : " 该节点当前无任何输出，请到源节点检查输出配置。";
                throw new InvalidOperationException(
                    $"{nodeLabel} 无输出变量 '{firstName}'（契约中不存在）。可用变量: [{available}]{renamedHint}");
            }

            object? current = variable.RuntimeValue;

            // 本路径是否含 ROI 类型过滤（[Type=X]）。ROI 字段投影路径（如 [Type=X][].Geometry.X）
            // 语义 = "该类型全部图形的该字段集合"，恒以数组返回（N=1 时也返回 double[1]，
            // 供几何参数订阅向量化拼装"有几个解析几个"）；因此后续段不做长度 1 解包。
            var isRoiTypedPath = firstTypeFilter != null;
            // A field projection owns its []/[n] on the final segment. Keep the
            // source collection intact until that segment has been evaluated.
            var hasExplicitProjectionIndex = parsedPath.Skip(1).Any(p => p.arrayIndex.HasValue);

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
            else if (firstTypeFilter == null && !hasExplicitProjectionIndex)
            {
                // 无索引且非 [Type=] 过滤 → 长度 1 数组默认取 [0]（存量标量绑定兼容，决策3）
                current = UnwrapLengthOne(current);
            }

            // 后续段：在当前运行时值上用反射逐段解析。
            // 支持"数组元素逐项投影"：当前值为过滤后的数组（如 [Type=X][] 保留的 RoiShape[]）
            // 且本段为纯成员访问（无索引/无类型过滤）时，对每个元素取该成员，返回投影数组。
            // 例：[Type=Circle][].Geometry.Radius → 所有圆的半径集合（double[N]，N=图形数）。
            for (int i = 1; i < parsedPath.Count; i++)
            {
                if (current == null)
                    throw new InvalidOperationException(
                        $"属性路径 '{propertyPath}' 中途遇到 null 值（取到 '{parsedPath[i].propertyName}' 时）");

                var (propName, idx, typeFilter) = parsedPath[i];

                // 数组逐元素投影：当前是数组、本段不取索引且不做类型过滤 → 对每个元素取成员并收拢为同构新数组。
                // 仅当元素自身可承载成员（非 double/string 等纯标量值数组——标量数组无成员访问）时投影；
                // 投影数组类型按首个成员结果类型创建（几何字段投影 double[]，避免泄漏 object[]）。
                if (current is not string && current is Array arr && typeFilter == null
                    && arr.Rank == 1
                    && !IsScalarValueArray(arr))
                {
                    var elementType = arr.GetType().GetElementType()!;
                    var memberType = elementType.GetProperty(propName)?.PropertyType
                        ?? elementType.GetField(propName)?.FieldType;
                    var firstMember = arr.Length > 0 ? ResolveMember(arr.GetValue(0), propName, propertyPath) : null;
                    if (arr.Length == 0 && memberType == null)
                        throw new InvalidOperationException($"属性路径 '{propertyPath}' 中成员 '{propName}' 不存在。");
                    var projected = Array.CreateInstance(firstMember?.GetType() ?? memberType ?? typeof(object), arr.Length);
                    if (arr.Length > 0) projected.SetValue(firstMember, 0);
                    for (int e = 1; e < arr.Length; e++)
                    {
                        projected.SetValue(ResolveMember(arr.GetValue(e), propName, propertyPath), e);
                    }
                    current = projected;
                }
                else
                {
                    current = ResolveMember(current, propName, propertyPath);
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
                else if (typeFilter == null && !isRoiTypedPath && !hasExplicitProjectionIndex)
                {
                    // 非 ROI 类型过滤路径保留"长度 1 数组解包为标量"（存量标量绑定兼容，决策3）。
                    // ROI 字段投影路径恒返回数组（长度 = 该类型图形数，含 1），供"有几个解析几个"向量化拼装。
                    current = UnwrapLengthOne(current);
                }
            }

            return current;
        }

        /// <summary>值数组（数值/字符串/布尔等）不存在可投影的实例成员，不做逐元素投影。</summary>
        private static bool IsScalarValueArray(Array arr)
        {
            var elemType = arr.GetType().GetElementType();
            if (elemType == null || !elemType.IsValueType)
                return false; // RoiShape/几何等引用元素数组 → 可投影（走成员反射，找不到时报错）
            return elemType == typeof(double) || elemType == typeof(int) || elemType == typeof(long)
                || elemType == typeof(float) || elemType == typeof(bool) || elemType == typeof(string)
                || elemType == typeof(byte) || elemType == typeof(short) || elemType == typeof(uint);
        }

        /// <summary>在当前值上取单个公共属性/字段（属性优先、字段回退），找不到即报错（rule-015）。</summary>
        private static object? ResolveMember(object? current, string propName, string propertyPath)
        {
            if (current == null)
                throw new InvalidOperationException(
                    $"属性路径 '{propertyPath}' 中途遇到 null 值（取到 '{propName}' 时）");

            var prop = current.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanRead)
            {
                try
                {
                    return prop.GetValue(current);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"属性路径 '{propertyPath}' 获取 '{propName}' 失败: {ex.Message}", ex);
                }
            }

            var field = current.GetType().GetField(propName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                var actualType = current.GetType().Name;
                throw new InvalidOperationException(
                    $"属性路径 '{propertyPath}' 中找不到属性或字段 '{propName}'（运行时值类型 '{actualType}' 无此公共实例属性/字段）");
            }
            return field.GetValue(current);
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
            if (current is not IEnumerable<RoiShape> roi)
                throw new InvalidOperationException(
                    $"属性 '{name}' 指定了 [Type={typeToken}] 类型过滤，但运行时值不是 RoiShape[]（实际类型: {current?.GetType().Name ?? "null"}）");

            if (!Enum.TryParse<ShapeType>(typeToken, out var targetType))
                throw new InvalidOperationException(
                    $"属性 '{name}' 的 [Type={typeToken}] 不是有效的图形类型（可用: Circle/Rectangle/RotatedRectangle/Polygon/Annulus/Arc）");

            var filtered = roi.Where(shape => shape.ShapeType == targetType).ToArray();
            if (filtered.Length == 0)
                throw new InvalidOperationException(
                    $"属性 '{name}' 按 [Type={typeToken}] 过滤后无匹配元素（数组共 {roi.Count()} 项）");

            return filtered;
        }
    }
}
