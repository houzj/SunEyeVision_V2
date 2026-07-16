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
                        throw new InvalidOperationException(
                            $"脚本输入 '{input.Name}' 绑定了节点属性 '{input.BindingSource}'，" +
                            $"但当前执行上下文未注入上游节点结果。" +
                            $"请确保上游节点已执行后再运行脚本工具。");
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
        /// 通过输出变量契约（SSOT）+ OutputVariableResolver 统一解析：
        /// 1. 先按 '.' 分割，前段为节点 ID，后段为属性路径
        /// 2. 由 OutputVariableResolver.Resolve() 在 GetOutputVariables() 中按 Name 寻址
        /// 3. 属性路径支持嵌套（C.D）和数组索引（C[0] / C[]）
        /// 4. 找不到时直接抛 InvalidOperationException（rule-015 禁止兜底回退）
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

            // 2. 通过输出变量契约解析（SSOT），找不到即报错
            var rawValue = OutputVariableResolver.Resolve(nodeResults, nodeId, propertyPath);

            // 3. 类型适配
            return CoerceNodeValue(rawValue, targetTypeName, inputName, nodeId, propertyPath);
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

            var targetType = SdkTypeVocabulary.ToSystemType(targetTypeName);

            // 决策1：数组→单值必须通过 [i] 索引（rule-015 禁止兜底回退）
            if (rawValue is Array && targetType != null && !targetType.IsArray)
            {
                throw new InvalidOperationException(
                    $"脚本输入 '{inputName}' 绑定的节点属性 '{nodeId}.{propertyPath}' 的值为数组类型，" +
                    $"不能直接赋值给单值字段 '{inputName}'（类型 '{targetTypeName}'）。" +
                    $"请在绑定路径末尾添加 [i] 索引，例如: {nodeId}.{propertyPath}[0]");
            }

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

            var targetType = SdkTypeVocabulary.ToSystemType(targetTypeName);

            // 决策1：数组→单值必须通过 [i] 索引（rule-015 禁止兜底回退）
            if (rawValue is Array && targetType != null && !targetType.IsArray)
            {
                throw new InvalidOperationException(
                    $"脚本输入 '{inputName}' 绑定的全局变量 '{varName}' 的值为数组类型，" +
                    $"不能直接赋值给单值字段 '{inputName}'（类型 '{targetTypeName}'）。" +
                    $"请在绑定路径末尾添加 [i] 索引，例如: {varName}[0]");
            }

            // 1. 类型完全匹配 → 直接返回
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
        /// 获取类型默认值，委派到 ScriptTypeMapper 统一管理。
        /// </summary>
        private static object? GetDefaultValue(string typeName)
        {
            return ScriptTypeMapper.GetDefaultValue(typeName);
        }
    }
}
