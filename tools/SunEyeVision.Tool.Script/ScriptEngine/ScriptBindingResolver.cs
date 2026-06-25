using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 脚本输入绑定解析器 — 解析 ScriptInputConfig.BindingSource 到实际值
    /// </summary>
    /// <remarks>
    /// 规则：
    /// 1. BindingSource 为空 → 返回类型默认值（不报错，用户未配置）
    /// 2. BindingSource 以 "__GlobalVars__." 开头 → 从 GlobalVariables 字典取值
    ///    - GlobalVariables == null → 报错（未注入全局变量字典）
    ///    - 变量不存在 → 报错（列出可用变量）
    /// 3. BindingSource 以 "NodeId.PropertyName" 格式 → 从 NodeResults 字典取上游节点结果对应属性值
    ///    - NodeResults 由 ToolExecutor 强制注入（工具调试 = 空字典，工作流执行 = 上游结果）
    ///    - 节点未执行 / 不在结果集中 → 报错（列出可用节点）
    ///    - 属性路径支持嵌套 (NodeId.A.B) 和数组索引 (NodeId.Prop[0] / NodeId.Prop[])
    /// 4. 其他格式 → 报错
    ///
    /// 类型适配（rule-008 报错优先 + 视觉软件行业标准宽松转换）：
    /// - 类型完全匹配 → 直接返回
    /// - 数字类型互转 → Convert.ChangeType
    /// - 字符串 → 数字/布尔 → 解析（InvariantCulture）
    /// - 解析失败 → 抛 InvalidOperationException（不静默回退）
    /// - 复杂类型（Image/Point/Rect 等）字符串无法转换 → 抛错
    ///
    /// 错误处理遵循 rule-008 报错优先：不静默回退到 null。
    ///
    /// 绑定解析统一行为：无论工作流执行还是工具调试，输入均通过绑定解析完成。
    /// 若上游节点未执行，解析出的值为空，直接报错即可。
    /// </remarks>
    internal static class ScriptBindingResolver
    {
        private const string GlobalVarsPrefix = "__GlobalVars__.";

        /// <summary>
        /// 解析输入绑定 → 字段值字典（key=字段名，value=实际值）
        /// </summary>
        /// <param name="inputs">输入配置列表</param>
        /// <param name="globalVariables">全局变量字典（可空）</param>
        /// <param name="nodeResults">上游节点执行结果字典（可空，未注入时节点属性绑定将报错）</param>
        /// <returns>字段名→值的字典</returns>
        /// <exception cref="InvalidOperationException">绑定源格式错误、目标不存在或类型不匹配</exception>
        public static Dictionary<string, object?> Resolve(
            IEnumerable<ScriptInputConfig> inputs,
            IDictionary<string, object?>? globalVariables,
            IDictionary<string, ToolResults>? nodeResults = null)
        {
            var result = new Dictionary<string, object?>();

            foreach (var input in inputs)
            {
                // 规则 1：未配置绑定 → 不加入结果字典，监视面板显示"无结果"
                // 脚本字段由 CLR 零初始化保证了有效初始值（int=0, bool=false, string=null）
                if (string.IsNullOrEmpty(input.BindingSource))
                {
                    continue;
                }

                // 规则 2：全局变量绑定
                if (input.BindingSource.StartsWith(GlobalVarsPrefix, StringComparison.Ordinal))
                {
                    var varName = input.BindingSource.Substring(GlobalVarsPrefix.Length);
                    if (string.IsNullOrEmpty(varName))
                        throw new InvalidOperationException(
                            $"脚本输入 '{input.Name}' 的绑定源格式错误: '{input.BindingSource}'（变量名不能为空）");

                    if (globalVariables == null)
                        throw new InvalidOperationException(
                            $"脚本输入 '{input.Name}' 绑定了全局变量 '{varName}'，但当前执行上下文未注入全局变量字典。");

                    if (!globalVariables.TryGetValue(varName, out var rawValue))
                    {
                        var available = globalVariables.Count > 0
                            ? "[" + string.Join(", ", globalVariables.Keys) + "]"
                            : "(空)";
                        throw new InvalidOperationException(
                            $"脚本输入 '{input.Name}' 绑定的全局变量 '{varName}' 不存在。可用全局变量: {available}");
                    }

                    // 类型适配：UI 文本框输入默认是 String，需转换到目标类型（Integer/Double/Bool/...）
                    result[input.Name] = CoerceValue(rawValue, input.TypeName, input.Name, varName);
                    continue;
                }

                // 规则 3：节点属性绑定 — NodeId.PropertyName[/Sub.Property][N]
                // ParamBinding 让用户能从下拉树选择上游节点的输出属性，
                // 此处必须支持完整解析（嵌套 + 数组索引 + 弱类型回退）。
                if (input.BindingSource.Contains('.'))
                {
                    if (nodeResults == null)
                    {
                        PluginLogger.Warning(
                            $"脚本输入 '{input.Name}' 绑定了节点属性 '{input.BindingSource}'，" +
                            $"但上游节点尚未执行，未获取到节点结果，已使用默认值继续。",
                            nameof(ScriptBindingResolver));
                        result[input.Name] = GetDefaultValue(input.TypeName);
                        continue;
                    }

                    result[input.Name] = ResolveNodeProperty(
                        input.BindingSource, input.TypeName, input.Name, nodeResults);
                    continue;
                }

                // 规则 4：未知格式
                throw new InvalidOperationException(
                    $"脚本输入 '{input.Name}' 的绑定源格式无法识别: '{input.BindingSource}'。" +
                    $"支持格式: 空（使用默认值）、__GlobalVars__.变量名、{{NodeId}}.{{Property}}");
            }

            return result;
        }

        /// <summary>
        /// 解析节点属性绑定：NodeId.PropertyName[/Sub.Property][N]
        /// </summary>
        /// <remarks>
        /// 复用 PropertyPathParser + AlgorithmNode.GetPropertyValueFromResult 的取值模式：
        /// 1. 先按 '.' 分割，前段为节点 ID，后段为属性路径
        /// 2. 用反射在 nodeResult.GetType() 上找属性
        /// 3. 找不到时回退到 nodeResult.GetResultItems() 弱类型查询（对齐 GenericToolResults）
        /// 4. 属性路径支持嵌套（C.D）和数组索引（C[0] / C[]）
        ///
        /// 错误处理（rule-008）：
        /// - 节点未执行 / 不在结果集中 → 抛 InvalidOperationException 列出可用节点
        /// - 属性不存在 → 抛 InvalidOperationException（不静默回退到 null）
        /// - 数组索引越界 / 非数组使用索引 → 抛 InvalidOperationException
        /// </remarks>
        private static object? ResolveNodeProperty(
            string bindingSource,
            string targetTypeName,
            string inputName,
            IDictionary<string, ToolResults> nodeResults)
        {
            // 1. 拆分 NodeId 和 PropertyPath
            var firstDot = bindingSource.IndexOf('.');
            if (firstDot <= 0 || firstDot == bindingSource.Length - 1)
                throw new InvalidOperationException(
                    $"脚本输入 '{inputName}' 的节点属性绑定源格式错误: '{bindingSource}'" +
                    $"（期望: NodeId.PropertyName）");

            var nodeId = bindingSource.Substring(0, firstDot);
            var propertyPath = bindingSource.Substring(firstDot + 1);

            // 2. 查找节点结果
            if (!nodeResults.TryGetValue(nodeId, out var nodeResult) || nodeResult == null)
            {
                var available = nodeResults.Count > 0
                    ? "[" + string.Join(", ", nodeResults.Keys) + "]"
                    : "(空)";
                throw new InvalidOperationException(
                    $"脚本输入 '{inputName}' 绑定的节点 '{nodeId}' 不在执行结果中。可用节点: {available}。" +
                    $"该节点必须先于脚本工具执行才能获取其输出结果。");
            }

            // 3. 沿属性路径取值（复用 PropertyPathParser + 反射 + GetResultItems 弱类型回退）
            var parsedPath = PropertyPathParser.ParsePropertyPath(propertyPath);
            if (parsedPath.Count == 0)
                throw new InvalidOperationException(
                    $"脚本输入 '{inputName}' 的属性路径为空: '{bindingSource}'");

            object? current = nodeResult;
            foreach (var (propName, arrayIndex) in parsedPath)
            {
                if (current == null)
                    throw new InvalidOperationException(
                        $"脚本输入 '{inputName}' 的属性路径 '{propertyPath}' 中途遇到 null 值（取到属性 '{propName}' 时）");

                current = GetPropertyOrResultItem(current, propName, inputName, propertyPath);

                // 数组索引处理
                if (current is null)
                {
                    // null 值 + 需要索引 → 索引语法对 null 无意义
                    if (arrayIndex.HasValue)
                        throw new InvalidOperationException(
                            $"脚本输入 '{inputName}' 的属性 '{propName}' 为 null，无法使用索引 [{arrayIndex.Value}]");
                    // 无索引要求 → 直接保留 null（供下游 CoerceNodeValue 处理）
                    continue;
                }

                if (current is Array arr)
                {
                    if (!arrayIndex.HasValue)
                    {
                        // [] 或无括号 → 返回整个数组（current 不变）
                        continue;
                    }

                    if (arrayIndex.Value < 0 || arrayIndex.Value >= arr.Length)
                        throw new InvalidOperationException(
                            $"脚本输入 '{inputName}' 的属性 '{propName}' 数组索引 {arrayIndex.Value} 越界 (长度={arr.Length})");

                    current = arr.GetValue(arrayIndex.Value);
                }
                else if (arrayIndex.HasValue)
                {
                    var actualType = current.GetType().Name;
                    throw new InvalidOperationException(
                        $"脚本输入 '{inputName}' 的属性 '{propName}' 不是数组，无法使用索引 [{arrayIndex.Value}]。" +
                        $"实际类型: {actualType}");
                }
            }

            // 4. 类型适配（与全局变量一致：完全匹配直接返回，否则按视觉软件行业标准宽松转换）
            return CoerceNodeValue(current, targetTypeName, inputName, nodeId, propertyPath);
        }

        /// <summary>
        /// 取对象的属性值，弱类型回退到 GetResultItems()
        /// </summary>
        private static object? GetPropertyOrResultItem(
            object current, string propName, string inputName, string propertyPath)
        {
            var type = current.GetType();
            var property = type.GetProperty(
                propName, BindingFlags.Public | BindingFlags.Instance);

            if (property != null && property.CanRead)
            {
                try
                {
                    return property.GetValue(current);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"脚本输入 '{inputName}' 获取属性 '{propName}' (路径 '{propertyPath}') 失败: {ex.Message}", ex);
                }
            }

            // 弱类型回退：GenericToolResults 等通过 GetResultItems() 暴露命名值
            if (current is ToolResults tr)
            {
                var foundItem = tr.GetResultItems()
                    .FirstOrDefault(i => string.Equals(i.Name, propName, StringComparison.OrdinalIgnoreCase));
                if (foundItem != null)
                    return foundItem.Value;
            }

            throw new InvalidOperationException(
                $"脚本输入 '{inputName}' 的属性路径 '{propertyPath}' 中找不到属性 '{propName}'" +
                $"(在 {type.Name} 上既没有该公共属性，也不在 GetResultItems() 中)");
        }

        /// <summary>
        /// 节点属性值类型适配（与 CoerceValue 行为一致，但错误信息针对节点属性）
        /// </summary>
        private static object? CoerceNodeValue(
            object? rawValue, string targetTypeName, string inputName, string nodeId, string propertyPath)
        {
            PluginLogger.Info(
                $"[CoerceDiag][Node] input='{inputName}' node='{nodeId}.{propertyPath}' " +
                $"rawType={(rawValue?.GetType().FullName ?? "null")} " +
                $"rawToString='{rawValue}' " +
                $"targetTypeName='{targetTypeName}'",
                nameof(ScriptBindingResolver));

            if (rawValue == null) return null;

            var targetType = GlobalVariableTypeMapper.ToSystemType(targetTypeName);
            if (targetType != null && targetType.IsInstanceOfType(rawValue))
                return rawValue;

            if (targetType != null && NumericTypes.IsNumericType(rawValue.GetType()) && NumericTypes.IsNumericType(targetType))
                return Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);

            if (rawValue is string str)
            {
                if (targetType == typeof(int) && int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vi)) return vi;
                if (targetType == typeof(double) && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var vd)) return vd;
                if (targetType == typeof(float) && float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var vf)) return vf;
                if (targetType == typeof(byte) && byte.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vb)) return vb;
                if (targetType == typeof(bool) && bool.TryParse(str, out var vbool)) return vbool;
                if (targetType == typeof(string)) return str;

                if (targetType != null && !IsSimpleType(targetType))
                {
                    throw new InvalidOperationException(
                        $"脚本输入 '{inputName}' 绑定的节点属性 '{nodeId}.{propertyPath}' 类型为 String，" +
                        $"但字段 '{inputName}' 声明为复杂类型 '{targetTypeName}'，" +
                        $"无法从字符串转换。");
                }

                throw new InvalidOperationException(
                    $"脚本输入 '{inputName}' 绑定的节点属性 '{nodeId}.{propertyPath}' 值为字符串 \"{str}\"，" +
                    $"无法解析为 '{targetTypeName}' 类型。");
            }

            throw new InvalidOperationException(
                $"脚本输入 '{inputName}' 绑定的节点属性 '{nodeId}.{propertyPath}' 类型为 '{rawValue.GetType().Name}'，" +
                $"与字段类型 '{targetTypeName}' 不匹配，且无法自动转换。");
        }

        /// <summary>
        /// 类型适配：把源值转换到目标类型（用于 UI 文本框字符串 → 数字的常见场景）
        /// </summary>
        /// <remarks>
        /// 视觉软件行业标准做法（Halcon、VisionPro、VisionMaster 都采用）：
        /// - 数字类型之间 → Convert.ChangeType
        /// - 字符串 → 数字/布尔 → 解析（InvariantCulture 避免本地化问题）
        /// - 解析失败 → 抛错（rule-008 报错优先，不静默回退）
        /// - 复杂类型（Image/Point/Rect 等）→ 不做转换，要求 GlobalVariable 类型与字段类型一致
        /// </remarks>
        private static object? CoerceValue(object? rawValue, string targetTypeName, string inputName, string varName)
        {
            PluginLogger.Info(
                $"[CoerceDiag][Var] input='{inputName}' var='{varName}' " +
                $"rawType={(rawValue?.GetType().FullName ?? "null")} " +
                $"rawToString='{rawValue}' " +
                $"targetTypeName='{targetTypeName}'",
                nameof(ScriptBindingResolver));

            if (rawValue == null) return null;

            // 1. 类型完全匹配 → 直接返回
            var targetType = GlobalVariableTypeMapper.ToSystemType(targetTypeName);
            if (targetType != null && targetType.IsInstanceOfType(rawValue))
                return rawValue;

            // 2. 数字类型互转（int → double, long → int 等）
            if (targetType != null && NumericTypes.IsNumericType(rawValue.GetType()) && NumericTypes.IsNumericType(targetType))
                return Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);

            // 3. 字符串 → 目标类型（关键：UI 文本框输入统一是 String）
            if (rawValue is string str)
            {
                if (targetType == typeof(int))
                {
                    if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        return v;
                }
                else if (targetType == typeof(double))
                {
                    if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        return v;
                }
                else if (targetType == typeof(float))
                {
                    if (float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        return v;
                }
                else if (targetType == typeof(byte))
                {
                    if (byte.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        return v;
                }
                else if (targetType == typeof(bool))
                {
                    if (bool.TryParse(str, out var v)) return v;
                }
                else if (targetType == typeof(string))
                {
                    return str;
                }
                else
                {
                    // 复杂类型（Image/Point/Rect/Circle/...）不接受字符串
                    throw new InvalidOperationException(
                        $"脚本输入 '{inputName}' 绑定的全局变量 '{varName}' 类型为 String，" +
                        $"但字段 '{inputName}' 声明为复杂类型 '{targetTypeName}'，" +
                        $"无法从字符串转换。请将全局变量类型修改为 '{targetTypeName}' 后再绑定。");
                }

                // 字符串解析失败 → 报错（rule-008）
                throw new InvalidOperationException(
                    $"脚本输入 '{inputName}' 绑定的全局变量 '{varName}' 值为字符串 \"{str}\"，" +
                    $"无法解析为 '{targetTypeName}' 类型。请检查全局变量值。");
            }

            // 4. 其它类型不匹配 → 报错（rule-008 禁止兜底）
            throw new InvalidOperationException(
                $"脚本输入 '{inputName}' 绑定的全局变量 '{varName}' 类型为 '{rawValue.GetType().Name}'，" +
                $"与字段类型 '{targetTypeName}' 不匹配，且无法自动转换。");
        }

        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
        }

        /// <summary>
        /// 获取类型默认值（与 ScriptInputConfig.GenerateFieldCode 中的默认值保持一致）
        /// </summary>
        private static object? GetDefaultValue(string typeName)
        {
            return typeName switch
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
                _             => null
            };
        }
    }
}
