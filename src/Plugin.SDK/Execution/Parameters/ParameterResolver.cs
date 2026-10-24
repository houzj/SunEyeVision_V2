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
    public partial class ParameterResolver : IParameterResolver
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
                // P1「抠几何」：ROI 容器 → 目标几何数组（Circle2d[]/Point2d[]/…）；
                // 区域订阅（目标词目 ROI）与数值目标不接管，保持原值走下方类型转换。
                if (GeometryExtractor.TryExtract(result.Value, targetType, out var geometryValue, out var geometryError))
                {
                    if (geometryError != null)
                    {
                        result.IsSuccess = false;
                        result.ErrorMessage = geometryError;
                        return result;
                    }

                    result.Value = geometryValue;
                    result.ValueType = targetType;
                    return result;
                }

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

            // 通过输出变量契约解析（唯一路径，无反射、无 GetResultItems 兜底）
            var value = OutputVariableResolver.Resolve(nodeResults, binding.SourceNodeId!, binding.SourceProperty!);

            var result = ParameterResolveResult.Success(value, binding.TargetType);
            result.SourceNodeId = binding.SourceNodeId;
            result.SourceProperty = binding.SourceProperty;

            return result;
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

                if (targetType.IsArray && targetType.GetArrayRank() == 1 && value is not Array &&
                    BindingContractValidator.Accept(sourceType, new[] { targetType }) == true)
                {
                    var element = ConvertValue(value, targetType.GetElementType()!);
                    if (!element.IsSuccess) return element;
                    var promoted = Array.CreateInstance(targetType.GetElementType()!, 1);
                    promoted.SetValue(element.Value, 0);
                    return ParameterResolveResult.Success(promoted, targetType);
                }

                // Outputs use arrays even for a single value. Adapt only at the typed
                // scalar input boundary, without changing the stored binding expression.
                if (value is Array array && !targetType.IsArray && targetType != typeof(object) &&
                    BindingContractValidator.Accept(sourceType, new[] { targetType }) == true)
                {
                    if (array.Rank != 1 || array.Length != 1)
                        return ParameterResolveResult.Failure(
                            $"当前输入需要单值，但绑定数组包含 {array.Length} 个元素；仅单元素数组可自动适配，请明确选择元素或使用集合输入。");
                    return ConvertValue(array.GetValue(0)!, targetType);
                }

                // 相同类型
                if (sourceType == targetType)
                {
                    return ParameterResolveResult.Success(value, targetType);
                }

                if (value is Array items && items.Rank == 1 && targetType.IsArray && targetType.GetArrayRank() == 1 &&
                    BindingContractValidator.Accept(sourceType, new[] { targetType }) == true)
                {
                    var converted = Array.CreateInstance(targetType.GetElementType()!, items.Length);
                    for (var index = 0; index < items.Length; index++)
                    {
                        var item = ConvertValue(items.GetValue(index)!, targetType.GetElementType()!);
                        if (!item.IsSuccess) return item;
                        converted.SetValue(item.Value, index);
                    }
                    return ParameterResolveResult.Success(converted, targetType);
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
