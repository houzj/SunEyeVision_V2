using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;

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
    /// 3. 嵌套属性访问（如 Center.X）
    /// 4. 类型转换
    /// </remarks>
    public class ParameterResolver : IParameterResolver
    {
        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger? _logger;

        /// <summary>
        /// 数据源查询服务（可选）
        /// </summary>
        private readonly IDataSourceQueryService? _dataQueryService;

        /// <summary>
        /// 创建参数解析器
        /// </summary>
        /// <param name="logger">日志记录器（可选）</param>
        public ParameterResolver(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 创建参数解析器（带数据源查询服务）
        /// </summary>
        /// <param name="dataQueryService">数据源查询服务</param>
        /// <param name="logger">日志记录器（可选）</param>
        public ParameterResolver(IDataSourceQueryService? dataQueryService, ILogger? logger = null)
        {
            _dataQueryService = dataQueryService;
            _logger = logger;
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
        public ParameterApplyResult ApplyBindings(
            ToolParameters parameters,
            IDictionary<string, ToolResults> nodeResults)
        {
            var result = new ParameterApplyResult { IsSuccess = true };
            var paramType = parameters.GetType();
            var properties = paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // 🔍 关键日志：记录解析开始
            _logger?.Log(LogLevel.Info, 
                $"开始应用参数绑定: {paramType.Name}, 共 {properties.Length} 个属性, nodeResults 包含 {nodeResults.Count} 个节点", 
                nameof(ParameterResolver));

            foreach (var property in properties)
            {
                // 只处理 ParamValue<T> 类型
                if (!IsParamValueType(property.PropertyType))
                    continue;

                var paramValue = property.GetValue(parameters) as ParamValueBase;
                if (paramValue == null)
                    continue;

                // 🔍 关键日志：检查绑定配置状态
                _logger?.Log(LogLevel.Info, 
                    $"检查属性 {property.Name} (类型: {property.PropertyType.Name}), BindingConfig = {(paramValue.BindingConfig != null ? "有值" : "null")}", 
                    nameof(ParameterResolver));

                // 只处理 Binding 模式
                var bindingConfig = paramValue.BindingConfig;
                if (bindingConfig == null || bindingConfig.BindingType != BindingType.Binding)
                    continue;

                _logger?.Log(LogLevel.Info, 
                    $"发现绑定参数: {property.Name}, 绑定到: {bindingConfig.SourceNodeId}.{bindingConfig.SourceProperty}", 
                    nameof(ParameterResolver));

                // 解析绑定值
                var resolveResult = Resolve(bindingConfig, nodeResults);
                result.Results[property.Name] = resolveResult;

                if (!resolveResult.IsSuccess)
                {
                    result.Errors.Add($"解析参数 {property.Name} 失败: {resolveResult.ErrorMessage}");
                    result.FailedCount++;
                    continue;
                }

                if (resolveResult.Value == null)
                {
                    result.Warnings.Add($"参数 {property.Name} 绑定解析值为 null");
                    result.FailedCount++;
                    continue;
                }

                // 关键修复：从 ParamValue<T> 的泛型参数 T 获取目标类型
                var targetType = property.PropertyType.GetGenericArguments()[0];
                var value = resolveResult.Value;

                _logger?.Log(LogLevel.Info, 
                    $"参数 {property.Name} 目标类型: {targetType.Name}, 解析结果类型: {value?.GetType().Name ?? "null"}", 
                    nameof(ParameterResolver));

                // 类型转换
                if (value != null && value.GetType() != targetType)
                {
                    _logger?.Log(LogLevel.Info, 
                        $"参数 {property.Name} 需要类型转换: {value.GetType().Name} -> {targetType.Name}", 
                        nameof(ParameterResolver));
                    var convertResult = ConvertValue(value, targetType);
                    if (convertResult.IsSuccess)
                    {
                        value = convertResult.Value;
                    }
                    else
                    {
                        result.Warnings.Add($"参数 {property.Name} 类型转换失败: {convertResult.ErrorMessage}");
                        result.FailedCount++;
                        continue;
                    }
                }

                // 设置值
                paramValue.ObjectValue = value;
                result.AppliedCount++;
            }

            if (result.FailedCount > 0)
                result.IsSuccess = false;

            return result;
        }

        /// <summary>
        /// 检查类型是否为 ParamValue&amp;lt;&gt;
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
            // 🔍 关键日志：开始解析动态绑定
            _logger?.Log(LogLevel.Info, 
                $"解析动态绑定: {binding.ParameterName} <- {binding.SourceNodeId}.{binding.SourceProperty}", 
                nameof(ParameterResolver));

            if (string.IsNullOrEmpty(binding.SourceNodeId))
            {
                _logger?.Log(LogLevel.Warning, 
                    $"参数 {binding.ParameterName}: 源节点ID为空", 
                    nameof(ParameterResolver));
                return ParameterResolveResult.Failure("源节点ID为空");
            }

            if (string.IsNullOrEmpty(binding.SourceProperty))
            {
                _logger?.Log(LogLevel.Warning, 
                    $"参数 {binding.ParameterName}: 源属性名称为空", 
                    nameof(ParameterResolver));
                return ParameterResolveResult.Failure("源属性名称为空");
            }

            // 获取源节点结果
            if (!nodeResults.TryGetValue(binding.SourceNodeId!, out var nodeResult))
            {
                // 🔍 关键日志：列出 nodeResults 中可用的节点ID
                var availableNodes = string.Join(", ", nodeResults.Keys);
                _logger?.Log(LogLevel.Warning, 
                    $"参数 {binding.ParameterName}: 源节点 {binding.SourceNodeId} 不存在于结果缓存中. 可用节点: [{availableNodes}]", 
                    nameof(ParameterResolver));
                return ParameterResolveResult.Failure($"源节点 {binding.SourceNodeId} 不存在于结果缓存中");
            }

            // 🔍 关键日志：节点存在，检查执行状态
            _logger?.Log(LogLevel.Info, 
                $"源节点 {binding.SourceNodeId} 存在, IsSuccess={nodeResult.IsSuccess}", 
                nameof(ParameterResolver));

            // 检查执行状态
            if (!nodeResult.IsSuccess)
            {
                _logger?.Log(LogLevel.Warning, 
                    $"参数 {binding.ParameterName}: 源节点 {binding.SourceNodeId} 执行未成功: {nodeResult.ErrorMessage}", 
                    nameof(ParameterResolver));
                return ParameterResolveResult.Failure($"源节点 {binding.SourceNodeId} 执行未成功: {nodeResult.ErrorMessage}");
            }

            // 🔍 关键日志：尝试获取属性值
            _logger?.Log(LogLevel.Info, 
                $"尝试获取属性: {binding.SourceProperty} 从节点 {binding.SourceNodeId}", 
                nameof(ParameterResolver));

            // 获取属性值
            var value = GetPropertyValue(nodeResult, binding.SourceProperty!);
            
            if (value == null)
            {
                _logger?.Log(LogLevel.Warning, 
                    $"属性 {binding.SourceProperty} 获取结果为 null", 
                    nameof(ParameterResolver));
            }
            else
            {
                _logger?.Log(LogLevel.Info, 
                    $"属性 {binding.SourceProperty} 获取成功: {value.GetType().Name} = {value}", 
                    nameof(ParameterResolver));
            }

            if (value == null && !binding.SourceProperty!.Contains('.'))
            {
                // 尝试从结果项获取（使用反射，统一使用新机制）
                var property = nodeResult.GetType().GetProperty(binding.SourceProperty);
                if (property != null && property.CanRead)
                {
                    value = property.GetValue(nodeResult);
                    _logger?.Log(LogLevel.Info, 
                        $"通过反射获取属性 {binding.SourceProperty}: {value?.GetType().Name ?? "null"}", 
                        nameof(ParameterResolver));
                }
            }

            // ★ 新增：从 GetResultItems() 中查找（支持 GenericToolResults 等基于结果项的弱类型类）
            if (value == null && nodeResult is ToolResults toolResult)
            {
                var resultItems = toolResult.GetResultItems();
                var foundItem = resultItems.FirstOrDefault(item =>
                    string.Equals(item.Name, binding.SourceProperty, StringComparison.OrdinalIgnoreCase));
                if (foundItem != null)
                {
                    value = foundItem.Value;
                    _logger?.Log(LogLevel.Info,
                        $"从 ResultItems 获取属性 {binding.SourceProperty}: {value?.GetType().Name ?? "null"}",
                        nameof(ParameterResolver));
                }
            }

            var result = ParameterResolveResult.Success(value, binding.TargetType);
            result.SourceNodeId = binding.SourceNodeId;
            result.SourceProperty = binding.SourceProperty;

            return result;
        }

        /// <summary>
        /// 获取属性值（支持嵌套属性和数组索引）
        /// </summary>
        private object? GetPropertyValue(object obj, string propertyPath)
        {
            if (obj == null || string.IsNullOrEmpty(propertyPath))
                return null;

            // 🔍 关键日志：解析属性路径
            var parsedPath = PropertyPathParser.ParsePropertyPath(propertyPath);
            _logger?.Log(LogLevel.Info, 
                $"解析属性路径: {propertyPath} -> {string.Join(" -> ", parsedPath.Select(p => $"{p.propertyName}{(p.arrayIndex.HasValue ? $"[{p.arrayIndex.Value}]" : "")}"))}", 
                nameof(ParameterResolver));
            
            object? current = obj;

            foreach (var (propName, arrayIndex) in parsedPath)
            {
                if (current == null)
                    return null;

                var type = current.GetType();
                var property = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                {
                    // 🔍 关键日志：属性不存在
                    _logger?.Log(LogLevel.Warning, 
                        $"属性 {propName} 在类型 {type.Name} 中不存在", 
                        nameof(ParameterResolver));
                    
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
                    _logger?.Log(LogLevel.Info, 
                        $"获取属性 {propName}: {current?.GetType().Name ?? "null"}", 
                        nameof(ParameterResolver));

                    // 如果当前值是数组，根据索引处理
                    if (current is Array array)
                    {
                        if (arrayIndex.HasValue)
                        {
                            // [0] [1] → 取指定索引的元素
                            if (arrayIndex.Value >= 0 && arrayIndex.Value < array.Length)
                                current = array.GetValue(arrayIndex.Value);
                            else
                            {
                                _logger?.Log(LogLevel.Warning, 
                                    $"数组索引 {arrayIndex.Value} 越界, 数组长度: {array.Length}", 
                                    nameof(ParameterResolver));
                                return null;  // 索引越界
                            }
                        }
                        // [] 或无括号 → 返回整个数组（current 不变）
                    }
                    else if (arrayIndex.HasValue)
                    {
                        // 非数组但有索引 → 类型错误
                        _logger?.Log(LogLevel.Warning, 
                            $"属性 {propName} 不是数组类型，但指定了索引 [{arrayIndex.Value}]", 
                            nameof(ParameterResolver));
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log(LogLevel.Warning, 
                        $"获取属性 {propName} 异常: {ex.Message}", 
                        nameof(ParameterResolver));
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
