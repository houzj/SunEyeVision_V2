using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Models;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 算法节点
    /// </summary>
    public class AlgorithmNode : WorkflowNodeBase
    {
        /// <summary>
        /// 工具实例
        /// </summary>
        public IToolPlugin? Tool { get; set; }

        /// <summary>
        /// 上次执行结果
        /// </summary>
        public AlgorithmResult LastResult { get; private set; }

        /// <summary>
        /// 上次工具执行结果（保留原始完整结果）
        /// </summary>
        public ToolResults? LastToolResult { get; private set; }

        public AlgorithmNode(string id, string name, string dispName, IToolPlugin tool)
            : base(id, name, dispName, "Algorithm")
        {
            Tool = tool;
        }

        /// <summary>
        /// 执行节点算法（带绑定解析）
        /// </summary>
        /// <param name="defaultInputImage">引擎通过拓扑传递的默认输入图像（回退使用）</param>
        /// <param name="nodeResults">其他节点的执行结果缓存（用于动态绑定解析）</param>
        public AlgorithmResult Execute(Mat? defaultInputImage, IDictionary<string, ToolResults>? nodeResults)
        {
            if (!IsEnabled)
            {
                return AlgorithmResult.CreateSuccess(defaultInputImage, 0);
            }

            OnBeforeExecute();

            try
            {
                if (Tool != null)
                {
                    var typedParams = GetTypedParameters();

                    // 1. 解析输入图像：优先使用参数绑定中的 ImageSource，回退到拓扑传递的默认图像
                    var inputImage = ResolveInputImage(typedParams, defaultInputImage, nodeResults);
                    typedParams.ResolvedInputImage = inputImage;

                    // 2. 应用参数绑定（从参数内部 BindingConfig 读取并解析）
                    if (nodeResults != null)
                    {
                        var resolver = new ParameterResolver(VisionLogger.Instance);
                        var applyResult = resolver.ApplyBindings(typedParams, nodeResults);

                        if (!applyResult.IsSuccess && applyResult.Errors.Count > 0)
                        {
                            var errors = string.Join("; ", applyResult.Errors);
                            var result = AlgorithmResult.CreateError($"参数绑定解析失败: {errors}");
                            OnAfterExecute(result);
                            return result;
                        }
                    }

                    // 3. 执行工具（不再传递 image 参数）
                    var toolResult = Tool.Run(typedParams);
                    LastToolResult = toolResult;
                    LastResult = ConvertToAlgorithmResult(toolResult);
                }
                else
                {
                    LastResult = AlgorithmResult.CreateError("节点未配置工具实例");
                }

                OnAfterExecute(LastResult);
                return LastResult;
            }
            catch (Exception ex)
            {
                var result = AlgorithmResult.CreateError($"节点 {Name} 执行失败: {ex.Message}");
                OnAfterExecute(result);
                return result;
            }
        }

        /// <summary>
        /// 解析输入图像 — 优先使用参数绑定中的 ImageSource，回退到拓扑传递的默认图像
        /// </summary>
        private Mat? ResolveInputImage(ToolParameters parameters, Mat? defaultInputImage, IDictionary<string, ToolResults>? nodeResults)
        {
            // 优先级1：参数中的 ImageSource 绑定（AvailableDataSource）
            var imageSourceProp = parameters.GetType().GetProperty("ImageSource");
            if (imageSourceProp != null)
            {
                var imageSource = imageSourceProp.GetValue(parameters) as AvailableDataSource;
                if (imageSource != null && nodeResults != null
                    && nodeResults.TryGetValue(imageSource.SourceNodeId, out var sourceResult))
                {
                    var value = GetPropertyValueFromResult(sourceResult, imageSource.PropertyName);
                    if (value is Mat mat && !mat.Empty())
                    {
                        return mat;
                    }
                }
            }

            // 优先级2：引擎通过拓扑传递的默认图像
            return defaultInputImage;
        }

        /// <summary>
        /// 从工具结果中获取指定属性的值
        /// </summary>
        private object? GetPropertyValueFromResult(ToolResults result, string propertyName)
        {
            if (result == null || string.IsNullOrEmpty(propertyName))
                return null;

            // 通过反射获取属性值
            var property = result.GetType().GetProperty(propertyName);
            if (property != null && property.CanRead)
            {
                return property.GetValue(result);
            }

            return null;
        }

        /// <summary>
        /// 获取强类型参数
        /// </summary>
        /// <remarks>
        /// 直接返回节点的 Parameters 属性，避免反射转换开销。
        /// 参数已由工作流引擎在创建节点时设置正确的类型。
        /// </remarks>
        private ToolParameters GetTypedParameters()
        {
            // 如果参数已经是正确的类型，直接返回
            if (Tool != null && Parameters.GetType() == Tool.ParamsType)
                return Parameters;

            // ✅ 如果参数类型不匹配，直接抛出异常
            throw new InvalidOperationException(
                $"参数类型不匹配。工具 {ToolType} 期望 {Tool.ParamsType?.Name ?? "null"}, 实际为 {Parameters.GetType().Name}");
        }

        /// <summary>
        /// 将 ToolResults 转换为 AlgorithmResult
        /// </summary>
        private AlgorithmResult ConvertToAlgorithmResult(ToolResults result)
        {
            if (!result.IsSuccess)
            {
                return AlgorithmResult.CreateError(result.ErrorMessage ?? "处理失败");
            }

            // 直接调用 GetOutputImage()，子类已正确重写此方法
            var outputImage = result.GetOutputImage();

            // 如果没有输出图像，返回错误
            if (outputImage == null || outputImage.Empty())
            {
                return AlgorithmResult.CreateError("工具执行成功但没有输出图像");
            }

            var algorithmResult = AlgorithmResult.CreateSuccess(outputImage, result.ExecutionTimeMs);

            // 保留原始工具结果引用
            algorithmResult.ToolResults = result;

            return algorithmResult;
        }
    }
}
