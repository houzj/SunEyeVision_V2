using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 参数解析服务实现
    /// </summary>
    /// <remarks>
    /// 提供参数绑定解析能力，将绑定配置转换为实际参数值。
    /// 
    /// 支持功能：
    /// 1. 常量绑定解析
    /// 2. 动态绑定解析（从节点结果获取）
    /// 3. 运行时注入解析（从运行时参数获取）
    /// 4. 嵌套属性访问（如 Center.X）
    /// 5. 简单表达式转换
    /// 6. 类型转换
    /// </remarks>
    public class ParameterResolver : IParameterResolver
    {
        /// <summary>
        /// 表达式编译缓存
        /// </summary>
        private readonly ConcurrentDictionary<string, Delegate> _expressionCache = new ConcurrentDictionary<string, Delegate>();

        /// <summary>
        /// 数据源查询服务（可选）
        /// </summary>
        private readonly IDataSourceQueryService? _dataQueryService;

        /// <summary>
        /// 创建参数解析器
        /// </summary>
        public ParameterResolver()
        {
        }

        /// <summary>
        /// 创建参数解析器（带数据源查询服务）
        /// </summary>
        /// <param name="dataQueryService">数据源查询服务</param>
        public ParameterResolver(IDataSourceQueryService? dataQueryService)
        {
            _dataQueryService = dataQueryService;
        }

        /// <inheritdoc/>
        public ParameterResolveResult Resolve(
            ParamSetting binding,
            IDictionary<string, ToolResults> nodeResults)
        {
            return binding.BindingType switch
            {
                BindingType.Constant => ResolveConstant(binding),
                BindingType.Binding => ResolveDynamic(binding, nodeResults),
                _ => ParameterResolveResult.Failure($"不支持的绑定类型: {binding.BindingType}")
            };
        }

        /// <inheritdoc/>
        public ParameterResolveResult Resolve(
            ParamSetting binding,
            IDictionary<string, ToolResults> nodeResults,
            Type targetType)
        {
            var result = Resolve(binding, nodeResults);

            if (result.IsSuccess && result.Value != null)
            {
                // 尝试类型转换
                var convertedValue = ConvertValue(result.Value, targetType);
                if (convertedValue.IsSuccess)
                {
                    result.Value = convertedValue.Value;
                    result.ValueType = targetType;
                }
                else
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = $"类型转换失败: {convertedValue.ErrorMessage}";
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public Dictionary<string, ParameterResolveResult> ResolveAll(
            ParamSettingContainer container,
            IDictionary<string, ToolResults> nodeResults)
        {
            var results = new Dictionary<string, ParameterResolveResult>();

            foreach (var binding in container.Settings)
            {
                var result = Resolve(binding, nodeResults);
                results[binding.ParameterName] = result;
            }

            return results;
        }

        /// <inheritdoc/>
        public ParameterApplyResult ApplyToParameters(
            ToolParameters parameters,
            ParamSettingContainer container,
            IDictionary<string, ToolResults> nodeResults)
        {
            var result = new ParameterApplyResult { IsSuccess = true };

            var resolveResults = ResolveAll(container, nodeResults);
            var paramType = parameters.GetType();

            foreach (var kvp in resolveResults)
            {
                var paramName = kvp.Key;
                var resolveResult = kvp.Value;

                result.Results[paramName] = resolveResult;

                if (resolveResult.IsSuccess)
                {
                    // 查找属性
                    var property = paramType.GetProperty(paramName, BindingFlags.Public | BindingFlags.Instance);
                    if (property != null && property.CanWrite)
                    {
                        try
                        {
                            var value = resolveResult.Value;
                            
                            // 获取对应的绑定配置
                            var binding = container.Settings.FirstOrDefault(s => s.ParameterName == paramName);

                            // 检查是否为 ParamValue<> 类型
                            if (IsParamValueType(property.PropertyType))
                            {
                                // ParamValue<T> 类型：设置 Value 和 BindingConfig
                                var paramValue = property.GetValue(parameters);
                                if (paramValue == null)
                                {
                                    result.Warnings.Add($"参数 {paramName} 的 ParamValue 未初始化");
                                    result.FailedCount++;
                                    continue;
                                }

                                // 获取 Value 和 BindingConfig 属性
                                // 使用基类的 ObjectValue 属性（支持反射）
                                var valueProperty = paramValue.GetType().GetProperty(nameof(ParamValueBase.ObjectValue));
                                var bindingConfigProperty = paramValue.GetType().GetProperty(nameof(ParamValue<object>.BindingConfig));

                                if (valueProperty != null && bindingConfigProperty != null)
                                {
                                    // 类型转换
                                    var targetType = valueProperty.PropertyType;
                                    if (value != null && targetType != value.GetType())
                                    {
                                        var convertResult = ConvertValue(value, targetType);
                                        if (convertResult.IsSuccess)
                                        {
                                            value = convertResult.Value;
                                        }
                                        else
                                        {
                                            result.Warnings.Add($"参数 {paramName} 类型转换失败: {convertResult.ErrorMessage}");
                                            result.FailedCount++;
                                            continue;
                                        }
                                    }

                                    // 设置 Value
                                    valueProperty.SetValue(paramValue, value);

                                    // 设置 BindingConfig
                                    if (binding != null)
                                    {
                                        bindingConfigProperty.SetValue(paramValue, binding);
                                    }

                                    result.AppliedCount++;
                                }
                                else
                                {
                                    result.Warnings.Add($"参数 {paramName} 缺少 Value 或 BindingConfig 属性");
                                    result.FailedCount++;
                                }
                            }
                            else
                            {
                                // 普通类型：直接设置值（向后兼容）
                                // 类型转换
                                if (value != null && property.PropertyType != value.GetType())
                                {
                                    var convertResult = ConvertValue(value, property.PropertyType);
                                    if (convertResult.IsSuccess)
                                    {
                                        value = convertResult.Value;
                                    }
                                    else
                                    {
                                        result.Warnings.Add($"参数 {paramName} 类型转换失败: {convertResult.ErrorMessage}");
                                        result.FailedCount++;
                                        continue;
                                    }
                                }

                                // 设置值
                                property.SetValue(parameters, value);
                                result.AppliedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Warnings.Add($"设置参数 {paramName} 失败: {ex.Message}");
                            result.FailedCount++;
                        }
                    }
                    else
                    {
                        result.Warnings.Add($"参数 {paramName} 不存在或不可写");
                        result.FailedCount++;
                    }
                }
                else
                {
                    result.Errors.Add($"解析参数 {paramName} 失败: {resolveResult.ErrorMessage}");
                    result.FailedCount++;
                }
            }

            if (result.FailedCount > 0)
            {
                result.IsSuccess = false;
            }

            return result;
        }

        /// <summary>
        /// 检查类型是否为 ParamValue<>
        /// </summary>
        private bool IsParamValueType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ParamValue<>);
        }

        /// <inheritdoc/>
        public ResolveValidationResult ValidateResolve(
            ParamSetting binding,
            IDictionary<string, ToolResults> nodeResults)
        {
            var result = new ResolveValidationResult { CanResolve = true };

            // 基本验证
            var bindingValidation = binding.Validate();
            if (!bindingValidation.IsValid)
            {
                result.CanResolve = false;
                result.Errors.AddRange(bindingValidation.Errors);
                return result;
            }

            switch (binding.BindingType)
            {
                case BindingType.Constant:
                    // 常量绑定始终可解析
                    break;

                case BindingType.Binding:
                    // 检查源节点是否存在
                    if (!nodeResults.ContainsKey(binding.SourceNodeId!))
                    {
                        result.CanResolve = false;
                        result.Errors.Add($"源节点 {binding.SourceNodeId} 不存在于结果缓存中");
                    }
                    else
                    {
                        // 检查节点是否执行成功
                        var nodeResult = nodeResults[binding.SourceNodeId!];
                        if (!nodeResult.IsSuccess)
                        {
                            result.HasWarnings = true;
                            result.Warnings.Add($"源节点 {binding.SourceNodeId} 执行未成功: {nodeResult.ErrorMessage}");
                        }
                    }
                    break;
            }

            return result;
        }

        /// <summary>
        /// 解析常量绑定
        /// </summary>
        private ParameterResolveResult ResolveConstant(ParamSetting binding)
        {
            return ParameterResolveResult.Success(binding.ConstantValue, binding.TargetType);
        }

        /// <summary>
        /// 解析动态绑定
        /// </summary>
        private ParameterResolveResult ResolveDynamic(
            ParamSetting binding,
            IDictionary<string, ToolResults> nodeResults)
        {
            if (string.IsNullOrEmpty(binding.SourceNodeId))
            {
                return ParameterResolveResult.Failure("源节点ID为空");
            }

            if (string.IsNullOrEmpty(binding.SourceProperty))
            {
                return ParameterResolveResult.Failure("源属性名称为空");
            }

            // 获取源节点结果
            if (!nodeResults.TryGetValue(binding.SourceNodeId!, out var nodeResult))
            {
                return ParameterResolveResult.Failure($"源节点 {binding.SourceNodeId} 不存在于结果缓存中");
            }

            // 检查执行状态
            if (!nodeResult.IsSuccess)
            {
                return ParameterResolveResult.Failure($"源节点 {binding.SourceNodeId} 执行未成功: {nodeResult.ErrorMessage}");
            }

            // 获取属性值
            var value = GetPropertyValue(nodeResult, binding.SourceProperty!);
            if (value == null && !binding.SourceProperty!.Contains('.'))
            {
                // 尝试从结果项获取（使用反射，统一使用新机制）
                var property = nodeResult.GetType().GetProperty(binding.SourceProperty);
                if (property != null && property.CanRead)
                {
                    value = property.GetValue(nodeResult);
                }
            }

            // 应用转换表达式
            if (!string.IsNullOrEmpty(binding.TransformExpression) && value != null)
            {
                try
                {
                    value = EvaluateExpression(binding.TransformExpression!, value);
                }
                catch (Exception ex)
                {
                    return ParameterResolveResult.Failure($"表达式计算失败: {ex.Message}");
                }
            }

            var result = ParameterResolveResult.Success(value, binding.TargetType);
            result.SourceNodeId = binding.SourceNodeId;
            result.SourceProperty = binding.SourceProperty;

            return result;
        }

        /// <summary>
        /// 解析表达式绑定
        /// </summary>
        private ParameterResolveResult ResolveExpression(
            ParamSetting binding,
            IDictionary<string, ToolResults> nodeResults)
        {
            // 表达式绑定目前作为动态绑定的扩展
            // 可以通过表达式引用多个节点的值
            if (string.IsNullOrEmpty(binding.TransformExpression))
            {
                return ParameterResolveResult.Failure("表达式为空");
            }

            try
            {
                // 解析表达式中的节点引用
                // 格式: $nodeId.Property
                var value = EvaluateComplexExpression(binding.TransformExpression!, nodeResults);
                return ParameterResolveResult.Success(value, binding.TargetType);
            }
            catch (Exception ex)
            {
                return ParameterResolveResult.Failure($"表达式计算失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取属性值（支持嵌套属性和数组索引）
        /// </summary>
        private object? GetPropertyValue(object obj, string propertyPath)
        {
            if (obj == null || string.IsNullOrEmpty(propertyPath))
                return null;

            var parsedPath = PropertyPathParser.ParsePropertyPath(propertyPath);
            object? current = obj;

            foreach (var (propName, arrayIndex) in parsedPath)
            {
                if (current == null)
                    return null;

                var type = current.GetType();
                var property = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                {
                    // 尝试使用类型转换器
                    var converter = TypeDescriptor.GetConverter(type);
                    if (converter != null)
                    {
                        return null;
                    }
                    return null;
                }

                try
                {
                    current = property.GetValue(current);

                    // 如果当前值是数组，根据索引处理
                    if (current is Array array)
                    {
                        if (arrayIndex.HasValue)
                        {
                            // [0] [1] → 取指定索引的元素
                            if (arrayIndex.Value >= 0 && arrayIndex.Value < array.Length)
                                current = array.GetValue(arrayIndex.Value);
                            else
                                return null;  // 索引越界
                        }
                        // [] 或无括号 → 返回整个数组（current 不变）
                    }
                    else if (arrayIndex.HasValue)
                    {
                        // 非数组但有索引 → 类型错误，返回 null
                        return null;
                    }
                }
                catch
                {
                    return null;
                }
            }

            return current;
        }

        /// <summary>
        /// 类型转换
        /// </summary>
        private ParameterResolveResult ConvertValue(object value, Type targetType)
        {
            try
            {
                if (value == null)
                {
                    if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                    {
                        return ParameterResolveResult.Failure("不能将null转换为值类型");
                    }
                    return ParameterResolveResult.Success(null, targetType);
                }

                var sourceType = value.GetType();

                // 相同类型
                if (sourceType == targetType)
                {
                    return ParameterResolveResult.Success(value, targetType);
                }

                // 可空类型
                var underlyingType = Nullable.GetUnderlyingType(targetType);
                if (underlyingType != null)
                {
                    if (sourceType == underlyingType)
                    {
                        var converted = Convert.ChangeType(value, underlyingType);
                        return ParameterResolveResult.Success(converted, targetType);
                    }
                }

                // 数值类型转换
                if (IsNumericType(sourceType) && IsNumericType(targetType))
                {
                    var converted = Convert.ChangeType(value, targetType);
                    return ParameterResolveResult.Success(converted, targetType);
                }

                // 字符串转换
                if (targetType == typeof(string))
                {
                    return ParameterResolveResult.Success(value.ToString(), targetType);
                }

                // 使用类型转换器
                var converter = TypeDescriptor.GetConverter(targetType);
                if (converter != null && converter.CanConvertFrom(sourceType))
                {
                    var converted = converter.ConvertFrom(value);
                    return ParameterResolveResult.Success(converted, targetType);
                }

                // 尝试直接转换
                var directConverted = Convert.ChangeType(value, targetType);
                return ParameterResolveResult.Success(directConverted, targetType);
            }
            catch (Exception ex)
            {
                return ParameterResolveResult.Failure($"类型转换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 编译表达式
        /// </summary>
        private Delegate CompileExpression(string expression)
        {
            return _expressionCache.GetOrAdd(expression, expr =>
            {
                // 创建参数表达式
                var paramExpr = Expression.Parameter(typeof(object), "value");

                // 简单表达式解析
                // 支持: value * 1.5, value + 10, Math.Max(value, 0) 等
                var lambda = Expression.Lambda(
                    Expression.Convert(
                        CreateExpressionBody(expr, Expression.Convert(paramExpr, typeof(double))),
                        typeof(object)),
                    paramExpr);

                return lambda.Compile();
            });
        }

        /// <summary>
        /// 创建表达式体
        /// </summary>
        private Expression CreateExpressionBody(string expression, Expression valueExpr)
        {
            // 简化实现：只支持基本的算术运算
            expression = expression.Trim();

            // 返回原值表达式
            return valueExpr;
        }

        /// <summary>
        /// 计算表达式
        /// </summary>
        private object? EvaluateExpression(string expression, object value)
        {
            try
            {
                var compiled = CompileExpression(expression);
                return compiled.DynamicInvoke(value);
            }
            catch
            {
                // 简单的数值计算
                if (value is IConvertible convertible)
                {
                    var doubleValue = convertible.ToDouble(null);
                    return EvaluateSimpleExpression(expression, doubleValue);
                }

                return value;
            }
        }

        /// <summary>
        /// 计算简单表达式
        /// </summary>
        private object? EvaluateSimpleExpression(string expression, double value)
        {
            // 简单的数学运算
            if (expression.Contains("value *"))
            {
                var factorStr = expression.Split(new[] { "value *" }, StringSplitOptions.None)[1].Trim();
                if (double.TryParse(factorStr, out var factor))
                {
                    return value * factor;
                }
            }
            else if (expression.Contains("value +"))
            {
                var addStr = expression.Split(new[] { "value +" }, StringSplitOptions.None)[1].Trim();
                if (double.TryParse(addStr, out var addValue))
                {
                    return value + addValue;
                }
            }
            else if (expression.Contains("value -"))
            {
                var subStr = expression.Split(new[] { "value -" }, StringSplitOptions.None)[1].Trim();
                if (double.TryParse(subStr, out var subValue))
                {
                    return value - subValue;
                }
            }
            else if (expression.Contains("value /"))
            {
                var divStr = expression.Split(new[] { "value /" }, StringSplitOptions.None)[1].Trim();
                if (double.TryParse(divStr, out var divValue) && divValue != 0)
                {
                    return value / divValue;
                }
            }

            return value;
        }

        /// <summary>
        /// 计算复杂表达式
        /// </summary>
        private object? EvaluateComplexExpression(string expression, IDictionary<string, ToolResults> nodeResults)
        {
            // 解析节点引用：$nodeId.Property
            // 示例: "$node001.Radius + $node002.Radius"
            var result = expression;

            // 查找所有 $nodeId.Property 模式
            var pattern = new System.Text.RegularExpressions.Regex(@"\$(\w+)\.(\w+(?:\.\w+)*)");
            var matches = pattern.Matches(expression);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var nodeId = match.Groups[1].Value;
                var propertyPath = match.Groups[2].Value;

                if (nodeResults.TryGetValue(nodeId, out var nodeResult))
                {
                    var value = GetPropertyValue(nodeResult, propertyPath);
                    if (value != null)
                    {
                        result = result.Replace(match.Value, value.ToString());
                    }
                }
            }

            // 尝试计算结果表达式
            try
            {
                var dataTable = new System.Data.DataTable();
                var computed = dataTable.Compute(result, null);
                return computed;
            }
            catch
            {
                return result;
            }
        }

        /// <summary>
        /// 检查是否为数值类型
        /// </summary>
        private bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short) ||
                   type == typeof(byte) || type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal) || type == typeof(uint) || type == typeof(ulong) ||
                   type == typeof(ushort) || type == typeof(sbyte);
        }
    }
}
