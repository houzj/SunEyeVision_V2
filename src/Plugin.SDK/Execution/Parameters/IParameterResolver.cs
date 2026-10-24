using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 参数解析服务接口
    /// </summary>
    /// <remarks>
    /// 提供参数绑定解析能力，将绑定配置转换为实际参数值。
    /// 
    /// 核心功能：
    /// 1. 解析单个参数绑定
    /// 2. 批量解析参数绑定容器
    /// 3. 支持类型转换
    /// 4. 支持表达式计算
    /// 
    /// 使用示例：
    /// <code>
    /// // 创建参数解析器
    /// IParameterResolver resolver = new ParameterResolver(dataQueryService);
    /// 
    /// // 解析单个绑定
    /// var value = resolver.Resolve(binding, nodeResults);
    /// 
    /// // 批量解析
    /// var parameters = resolver.ResolveAll(bindingContainer, nodeResults);
    /// 
    /// // 应用到工具参数
    /// foreach (var param in parameters)
    /// {
    ///     toolParams.SetParameter(param.Key, param.Value);
    /// }
    /// </code>
    /// </remarks>
    public interface IParameterResolver : SunEyeVision.Plugin.Contracts.IBindingPlanCompiler
    {
        SunEyeVision.Plugin.Contracts.BindingResolution ResolveBinding(
            SunEyeVision.Plugin.Contracts.BindingPlan plan,
            SunEyeVision.Plugin.Contracts.IBindingRunSnapshot snapshot);
        /// <summary>
        /// 解析单个参数绑定
        /// </summary>
        /// <param name="binding">参数绑定</param>
        /// <param name="nodeResults">节点结果缓存</param>
        /// <returns>解析后的值</returns>
        ParameterResolveResult Resolve(
            ParamSetting binding,
            IDictionary<string, ToolResults> nodeResults);

        /// <summary>
        /// 解析单个参数绑定（带类型）
        /// </summary>
        /// <param name="binding">参数绑定</param>
        /// <param name="nodeResults">节点结果缓存</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>解析后的值</returns>
        ParameterResolveResult Resolve(
            ParamSetting binding,
            IDictionary<string, ToolResults> nodeResults,
            Type targetType);

        /// <summary>
        /// 批量解析参数绑定容器
        /// </summary>
        /// <param name="container">参数绑定容器</param>
        /// <param name="nodeResults">节点结果缓存</param>
        /// <returns>参数名到值的映射</returns>
        Dictionary<string, ParameterResolveResult> ResolveAll(
            ParamSettingContainer container,
            IDictionary<string, ToolResults> nodeResults);

/// <summary>
        /// 验证绑定是否可解析
        /// </summary>
        /// <param name="binding">参数绑定</param>
        /// <param name="nodeResults">节点结果缓存</param>
        /// <returns>验证结果</returns>
        ResolveValidationResult ValidateResolve(
            ParamSetting binding,
            IDictionary<string, ToolResults> nodeResults);
    }

    /// <summary>
    /// 参数解析结果
    /// </summary>
    public class ParameterResolveResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 解析后的值
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// 值类型
        /// </summary>
        public Type? ValueType { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 源节点ID（动态绑定时）
        /// </summary>
        public string? SourceNodeId { get; set; }

        /// <summary>
        /// 源属性名称（动态绑定时）
        /// </summary>
        public string? SourceProperty { get; set; }

        /// <summary>
        /// 是否使用默认值
        /// </summary>
        public bool UsedDefaultValue { get; set; }

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static ParameterResolveResult Success(object? value, Type? type = null)
        {
            return new ParameterResolveResult
            {
                IsSuccess = true,
                Value = value,
                ValueType = type ?? value?.GetType()
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static ParameterResolveResult Failure(string errorMessage)
        {
            return new ParameterResolveResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// 解析验证结果
    /// </summary>
    public class ResolveValidationResult
    {
        /// <summary>
        /// 是否可以解析
        /// </summary>
        public bool CanResolve { get; set; }

        /// <summary>
        /// 是否有警告
        /// </summary>
        public bool HasWarnings { get; set; }

        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
