using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.UI.Windows;
using SunEyeVision.Tool.Subroutine.Views;
using ExecutionResult = SunEyeVision.Plugin.SDK.Execution.Results.ExecutionResult;

namespace SunEyeVision.Tool.Subroutine
{
    /// <summary>
    /// 子程序工具 — 本质上是一个"大号工具"，
    /// 内部通过工作流引擎执行子工作流，将结果映射到标准 ToolResults 输出。
    /// </summary>
    [Tool(
        id: "Subroutine",
        displayName: "子程序",
        Description = "执行子工作流（大号工具，内部使用工作流节点组织）",
        Icon = "\U0001F501",
        Category = "工作流控制",
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class SubroutineTool : IToolPlugin<SubroutineParameters, SubroutineResults>
    {
        static SubroutineTool()
        {
            SubroutineDebugServiceProvider.Register(new SubroutineDebugServiceImpl());
        }
        /// <summary>
        /// 执行子程序逻辑
        /// </summary>
        public SubroutineResults Run(SubroutineParameters parameters)
        {
            var sw = Stopwatch.StartNew();
            var result = new SubroutineResults
            {
                ToolName = "子程序",
                ToolId = "Subroutine",
                Timestamp = DateTime.Now
            };

            // 单一 ParameterResolver 实例，消除 4 处重复 new
            var resolver = new ParameterResolver(parameters.Context?.Logger);
            var nodeResults = parameters.NodeResults;

            try
            {
                // ===== 前置验证 =====
                if (string.IsNullOrEmpty(parameters.SubroutineId))
                    throw new InvalidOperationException($"子程序 \"{parameters.SubroutineName}\" 未配置目标工作流");

                if (parameters.ExecuteSubWorkflowAsync == null)
                    throw new InvalidOperationException("子工作流执行委托未注入，请确保通过 WorkflowExecutionEngine 执行包含子程序节点的工作流");

                var inputImage = parameters.ResolvedInputImage;

                parameters.LogInfo($"开始执行子程序: {parameters.SubroutineName}, 工作流ID: {parameters.SubroutineId}, 输入图像: {(inputImage != null ? $"{inputImage.Width}x{inputImage.Height}" : "null")}");

                // ===== 解析输入参数绑定 =====
                var contextVariables = ResolveInputs(parameters, resolver);
                if (contextVariables.Count > 0)
                {
                    parameters.LogInfo($"  已解析 {contextVariables.Count} 个输入参数绑定");
                }

                // ===== 执行子工作流 =====
                ExecutionResult execResult;
                if (parameters.IsLoopEnabled)
                {
                    execResult = ExecuteWithLoop(parameters, inputImage, contextVariables, resolver, nodeResults);
                }
                else
                {
                    execResult = ExecuteSubWorkflowOnce(parameters, inputImage, contextVariables);
                }

                // ===== 收集结果 =====
                var subWorkflowNodeResults = new Dictionary<string, ToolResults>();
                if (execResult.NodeResults != null)
                {
                    foreach (var kvp in execResult.NodeResults)
                    {
                        if (kvp.Value?.Result != null)
                        {
                            result.NodeOutputs[kvp.Key] = kvp.Value.Result;
                            subWorkflowNodeResults[kvp.Key] = kvp.Value.Result;
                        }
                    }
                }

                result.OutputConfigs = parameters.Outputs.ToList();

                // ===== 解析输出项 =====
                if (subWorkflowNodeResults.Count > 0)
                {
                    var outputErrors = new List<string>();
                    foreach (var output in parameters.Outputs.Where(o => o.Source != null || !string.IsNullOrEmpty(o.BindingSource)))
                    {
                        string sourceNodeId, sourceProperty;
                        if (output.Source != null)
                        {
                            // 优先使用对象引用（含完整类型信息）
                            sourceNodeId = output.Source.SourceNodeId;
                            sourceProperty = output.Source.PropertyName;
                        }
                        else
                        {
                            // ★ 从 BindingSource 字符串 "NodeId.PropertyName" 解析（对齐 ResolveInputs）
                            var parts = output.BindingSource.Split('.');
                            if (parts.Length < 2)
                            {
                                outputErrors.Add($"输出 '{output.Name}' BindingSource 格式无效: '{output.BindingSource}'");
                                continue;
                            }
                            sourceNodeId = parts[0];
                            // 剥离旧数据遗留的 "[]" 后缀（对齐 AvailableDataSourceConverter 的做法）
                            sourceProperty = parts[1].EndsWith("[]") ? parts[1][..^2] : parts[1];
                        }

                        // 目标类型优先取对象引用声明的类型，否则用输出声明的 TypeName
                        var targetType = output.Source?.PropertyType
                            ?? SdkTypeVocabulary.ToSystemType(output.TypeName)
                            ?? typeof(object);

                        var binding = new ParamSetting
                        {
                            ParameterName = output.Name,
                            BindingType = BindingType.Binding,
                            SourceNodeId = sourceNodeId,
                            SourceProperty = sourceProperty,
                            TargetType = targetType
                        };
                        var resolveResult = resolver.Resolve(binding, subWorkflowNodeResults);
                        if (resolveResult.IsSuccess && resolveResult.Value != null)
                        {
                            result.ResolvedOutputValues[output.Name] = resolveResult.Value;
                            parameters.LogInfo($"  输出 '{output.Name}' = {sourceNodeId}.{sourceProperty} = {resolveResult.Value}");
                        }
                        else
                        {
                            outputErrors.Add($"输出 '{output.Name}' 绑定解析失败: {resolveResult.ErrorMessage ?? "值为空"}");
                        }
                    }

                    if (outputErrors.Count > 0)
                    {
                        var errorMsg = string.Join("; ", outputErrors);
                        parameters.LogError($"子程序输出绑定解析失败: {errorMsg}");
                        throw new InvalidOperationException(errorMsg);
                    }
                }

                // ===== 构建显示项（根据 DisplayConfigs 从子工作流节点结果提取图形）=====
                BuildDisplayItemsFromConfig(result, parameters, subWorkflowNodeResults, resolver);

                result.ExecutedNodeCount = execResult.NodeResults?.Count ?? 0;
                result.SubWorkflowExecutionTimeMs = (long)execResult.ExecutionTime.TotalMilliseconds;

                if (!execResult.Success && execResult.Errors != null && execResult.Errors.Any())
                {
                    var errorMsg = string.Join("; ", execResult.Errors);
                    parameters.LogError($"子程序执行失败: {errorMsg}");
                    result.SetError(errorMsg);
                }
                else
                {
                    sw.Stop();
                    result.SetSuccess(sw.ElapsedMilliseconds);
                    parameters.LogSuccess($"子程序执行完成: {result.ExecutedNodeCount} 个节点, 耗时 {result.SubWorkflowExecutionTimeMs}ms");
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                parameters.LogError($"子程序异常: {ex.Message}", ex);
                result.SetError($"子程序执行异常: {ex.Message}", ex);
            }

            return result;
        }

        private static ExecutionResult ExecuteSubWorkflowOnce(SubroutineParameters parameters, IVisionImage? inputImage,
            Dictionary<string, object> contextVariables)
        {
            if (parameters.ExecuteSubWorkflowAsync == null)
                throw new InvalidOperationException("子工作流执行委托未注入");

            return Task.Run(async () =>
                await parameters.ExecuteSubWorkflowAsync(parameters.SubroutineId, inputImage, contextVariables))
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// 解析输入参数 — 从父工作流的 NodeResults 中根据 Inputs[].Source 取值
        /// </summary>
        private static Dictionary<string, object> ResolveInputs(SubroutineParameters parameters, ParameterResolver resolver)
        {
            var values = new Dictionary<string, object>();
            var errors = new List<string>();
            var nodeResults = parameters.NodeResults;
            if (nodeResults == null) return values;

            foreach (var input in parameters.Inputs.Where(i => i.Source != null || !string.IsNullOrEmpty(i.BindingSource)))
            {
                string sourceNodeId, sourceProperty;
                string sourceNodeName = "?", displayName = "?";

                if (input.Source != null)
                {
                    // 优先使用对象引用（含完整类型信息）
                    sourceNodeId = input.Source.SourceNodeId;
                    sourceProperty = input.Source.PropertyName;
                    sourceNodeName = input.Source.SourceNodeName ?? "?";
                    displayName = input.Source.DisplayName ?? sourceProperty;
                }
                else
                {
                    // ★ 从 BindingSource 字符串 "NodeId.PropertyName" 解析
                    var parts = input.BindingSource.Split('.');
                    if (parts.Length < 2)
                    {
                        errors.Add($"输入 '{input.Name}' BindingSource 格式无效: '{input.BindingSource}'");
                        continue;
                    }
                    sourceNodeId = parts[0];
                    sourceProperty = parts[1];
                    sourceNodeName = input.BindingDisplayText?.Split('.') is { Length: >= 2 } displayParts
                        ? displayParts[0] : sourceNodeId;
                    displayName = sourceProperty;
                }

                var bindingKey = input.BindingKey ?? input.Name;
                var binding = new ParamSetting
                {
                    ParameterName = bindingKey,
                    BindingType = BindingType.Binding,
                    SourceNodeId = sourceNodeId,
                    SourceProperty = sourceProperty,
                    TargetType = typeof(object)
                };
                var resolveResult = resolver.Resolve(binding, nodeResults);
                if (resolveResult.IsSuccess && resolveResult.Value != null)
                {
                    values[bindingKey] = resolveResult.Value;
                    parameters.LogInfo($"  输入 '{input.Name}' <- {sourceNodeName}.{displayName} (来自 {(input.Source != null ? "Source对象" : "BindingSource字符串")})");
                }
                else
                {
                    errors.Add($"输入 '{input.Name}' 绑定解析失败: {resolveResult.ErrorMessage ?? "值为空"}");
                }
            }

            // ★ 默认图像输入：Image 类型输入端口未显式绑定上游时，
            //    使用子程序节点的输入图像（ResolvedInputImage），保证内部节点默认绑定可解析
            if (parameters.ResolvedInputImage != null)
            {
                foreach (var input in parameters.Inputs.Where(i =>
                    string.Equals(i.TypeName, "Image", StringComparison.OrdinalIgnoreCase)
                    && i.Source == null
                    && string.IsNullOrEmpty(i.BindingSource)))
                {
                    var bindingKey = input.BindingKey ?? input.Name;
                    if (!values.ContainsKey(bindingKey))
                    {
                        values[bindingKey] = parameters.ResolvedInputImage;
                        parameters.LogInfo($"  输入 '{input.Name}' 未显式绑定，默认使用子程序输入图像");
                    }
                }
            }

            if (errors.Count > 0)
            {
                var errorMsg = string.Join("; ", errors);
                parameters.LogError($"子程序输入参数绑定解析失败: {errorMsg}");
                throw new InvalidOperationException(errorMsg);
            }

            return values;
        }

        private static ExecutionResult ExecuteWithLoop(SubroutineParameters parameters, IVisionImage? inputImage,
            Dictionary<string, object> contextVariables, ParameterResolver resolver, IDictionary<string, ToolResults>? nodeResults)
        {
            var config = parameters.LoopConfig;
            var result = new ExecutionResult();

            int start = ResolveLoopBound(config.StartValueBinding, "循环起始值", resolver, nodeResults);
            int end = ResolveLoopBound(config.EndValueBinding, "循环结束值", resolver, nodeResults);

            parameters.LogInfo($"循环起止: [{start}, {end}), 中断循环: {config.EnableBreak}, 共 {end - start} 次");

            for (int i = start; i < end; i++)
            {
                parameters.CurrentIteration = i;
                var iterResult = ExecuteSubWorkflowOnce(parameters, inputImage, contextVariables);
                result.Merge(iterResult);

                if (!iterResult.Success)
                    break;

                // ===== 中断条件判断 =====
                if (config.EnableBreak)
                {
                    bool shouldBreak = EvaluateBreakCondition(config, resolver, nodeResults, iterResult, parameters);
                    if (shouldBreak)
                    {
                        parameters.LogInfo($"循环在第 {i} 次迭代后触发中断条件，提前结束");
                        break;
                    }
                }

                // ===== 循环间隔 =====
                if (config.LoopInterval > 0)
                    Task.Delay(config.LoopInterval).GetAwaiter().GetResult();
            }

            return result;
        }

        /// <summary>
        /// 解析循环边界值：必须通过绑定源获取，不支持字面量
        /// </summary>
        private static int ResolveLoopBound(string bindingPath, string displayName,
            ParameterResolver resolver, IDictionary<string, ToolResults>? nodeResults)
        {
            if (string.IsNullOrEmpty(bindingPath))
                throw new InvalidOperationException($"{displayName} 未配置绑定源，请先点击 🔗 选择上游节点输出");

            if (nodeResults == null)
                throw new InvalidOperationException($"节点结果为空，无法解析 {displayName} 绑定");

            var parts = bindingPath.Split('.');
            if (parts.Length < 2)
                throw new InvalidOperationException($"{displayName} 绑定源格式无效: '{bindingPath}'，应为 \"节点ID.属性名\"");

            var binding = new ParamSetting
            {
                ParameterName = displayName,
                BindingType = BindingType.Binding,
                SourceNodeId = parts[0],
                SourceProperty = parts[1],
                TargetType = typeof(int)
            };
            var resolveResult = resolver.Resolve(binding, nodeResults);
            if (!resolveResult.IsSuccess || resolveResult.Value == null)
                throw new InvalidOperationException($"{displayName} 绑定解析失败: {resolveResult.ErrorMessage ?? "值为空"}");

            return Convert.ToInt32(resolveResult.Value);
        }

        /// <summary>
        /// 评估循环中断条件：源/目的比较值均必须通过绑定获取
        /// </summary>
        private static bool EvaluateBreakCondition(SubroutineLoopConfig config,
            ParameterResolver resolver, IDictionary<string, ToolResults>? nodeResults,
            ExecutionResult iterResult, SubroutineParameters parameters)
        {
            if (!config.EnableBreak) return false;

            // 源比较值：从绑定源解析（当前迭代结果）
            string? sourceStr = null;
            if (!string.IsNullOrEmpty(config.SourceCompareBinding) && iterResult.NodeResults != null)
            {
                var parts = config.SourceCompareBinding.Split('.');
                if (parts.Length >= 2)
                {
                    var binding = new ParamSetting
                    {
                        ParameterName = "源比较值",
                        BindingType = BindingType.Binding,
                        SourceNodeId = parts[0],
                        SourceProperty = parts[1],
                        TargetType = typeof(double)
                    };
                    var iterToolResults = iterResult.NodeResults
                        .Where(kvp => kvp.Value?.Result != null)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Result!);
                    var resolveResult = resolver.Resolve(binding, iterToolResults);
                    if (resolveResult.IsSuccess && resolveResult.Value != null)
                    {
                        sourceStr = resolveResult.Value.ToString();
                    }
                }
            }
            if (string.IsNullOrEmpty(sourceStr))
            {
                parameters.LogWarning("中断条件: 源比较值未配置有效绑定源，不触发中断");
                return false;
            }

            // 目的比较值：从绑定源解析（父工作流）
            string? targetStr = null;
            if (!string.IsNullOrEmpty(config.TargetCompareBinding) && nodeResults != null)
            {
                var parts = config.TargetCompareBinding.Split('.');
                if (parts.Length >= 2)
                {
                    var binding = new ParamSetting
                    {
                        ParameterName = "目的比较值",
                        BindingType = BindingType.Binding,
                        SourceNodeId = parts[0],
                        SourceProperty = parts[1],
                        TargetType = typeof(double)
                    };
                    var resolveResult = resolver.Resolve(binding, nodeResults);
                    if (resolveResult.IsSuccess && resolveResult.Value != null)
                    {
                        targetStr = resolveResult.Value.ToString();
                    }
                }
            }
            if (string.IsNullOrEmpty(targetStr))
            {
                parameters.LogWarning("中断条件: 目的比较值未配置有效绑定源，不触发中断");
                return false;
            }

            // 类型转换后比较
            if (!double.TryParse(sourceStr, out var sourceVal) || !double.TryParse(targetStr, out var targetVal))
                return false;

            bool conditionMet = config.ComparisonOperator switch
            {
                ">" => sourceVal > targetVal,
                ">=" => sourceVal >= targetVal,
                "<" => sourceVal < targetVal,
                "<=" => sourceVal <= targetVal,
                "==" => Math.Abs(sourceVal - targetVal) < 1e-9,
                "!=" => Math.Abs(sourceVal - targetVal) >= 1e-9,
                _ => false
            };

            parameters.LogInfo($"  中断判断: {sourceStr} {config.ComparisonOperator} {targetStr} = {conditionMet}");
            return conditionMet;
        }

        /// <summary>
        /// 根据 DisplayConfigs 从子工作流节点结果中提取图形数据，推入 DisplayCollector。
        /// 对齐普通工具在 Run() 中主动构建显示项的模式。
        /// </summary>
        private static void BuildDisplayItemsFromConfig(
            SubroutineResults result,
            SubroutineParameters parameters,
            Dictionary<string, ToolResults> subWorkflowNodeResults,
            ParameterResolver resolver)
        {
            if (parameters.DisplayConfigs.Count == 0)
                return;

            foreach (var displayItem in parameters.DisplayConfigs)
            {
                if (string.IsNullOrEmpty(displayItem.BindingSource))
                    continue;

                var parts = displayItem.BindingSource.Split('.');
                if (parts.Length < 2)
                    continue;

                var binding = new ParamSetting
                {
                    ParameterName = displayItem.DisplayName,
                    BindingType = BindingType.Binding,
                    SourceNodeId = parts[0],
                    SourceProperty = parts[1],
                    TargetType = typeof(object)
                };

                var resolveResult = resolver.Resolve(binding, subWorkflowNodeResults);
                if (!resolveResult.IsSuccess || resolveResult.Value == null)
                {
                    parameters.LogWarning($"显示项 '{displayItem.DisplayName}' 绑定解析失败: {resolveResult.ErrorMessage ?? "值为空"}");
                    continue;
                }

                var value = resolveResult.Value;
                const bool isPass = true; // 子程序不代为评估内部节点通过/失败，显示属于子程序自身结果

                switch (displayItem.DisplayType)
                {
                    case SubroutineDisplayType.Point when value is Point pt:
                        result.Display.AddPoint(displayItem.GraphicsStyle, pt, isPass);
                        break;

                    case SubroutineDisplayType.Rectangle when value is Rect rect:
                        result.Display.AddRect(displayItem.GraphicsStyle, rect, isPass);
                        break;

                    case SubroutineDisplayType.Cross when value is Point crossPt:
                        result.Display.AddCross(displayItem.GraphicsStyle, crossPt, isPass);
                        break;

                    case SubroutineDisplayType.Text when displayItem.TextStyle != null:
                        result.Display.AddText(displayItem.TextStyle,
                            displayItem.TextStyle.PositionX, displayItem.TextStyle.PositionY,
                            value.ToString() ?? string.Empty, isPass);
                        break;

                    case SubroutineDisplayType.Circle:
                    case SubroutineDisplayType.Line:
                    case SubroutineDisplayType.Contour:
                    case SubroutineDisplayType.Polygon:
                    {
                        if (value is Point[] pts)
                        {
                            bool isClosed = displayItem.DisplayType != SubroutineDisplayType.Line;
                            result.Display.AddContour(displayItem.GraphicsStyle, pts, isPass, isClosed);
                        }
                        break;
                    }

                    case SubroutineDisplayType.Image:
                        // Image 由 GetDisplayImages() 处理，不加到 GraphicsDisplay
                        break;

                    default:
                        parameters.LogWarning($"显示项 '{displayItem.DisplayName}' 类型 {displayItem.DisplayType} 暂不支持自动映射");
                        break;
                }
            }
        }
    }

    /// <summary>
    /// 子程序调试服务实现 — 在 SubroutineTool 加载时注册到 SubroutineDebugServiceProvider。
    /// UI 通过此服务创建调试窗口，不依赖 Subroutine 工具程序集的具体类型。
    /// </summary>
    internal class SubroutineDebugServiceImpl : ISubroutineDebugService
    {
        public DefaultDebugWindow CreateDebugWindow(
            System.Windows.Window owner,
            string subroutineId,
            string subroutineName,
            string nodeId,
            IDataSourceQueryService? queryService,
            SubroutineParameters parameters,
            List<AvailableDataSource>? subroutineInternalSources,
            Action? onOutputsChanged)
        {
            var control = new SubroutineDebugControl(
                subroutineId, subroutineName, nodeId,
                queryService, subroutineInternalSources,
                parameters, onOutputsChanged);

            return new DefaultDebugWindow(control)
            {
                Owner = owner
            };
        }
    }
}
