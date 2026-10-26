using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
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
        Icon = "subroutine", Order = 20,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Control,
        Version = "1.0.0",
        HasDebugWindow = true
    )]
    public class SubroutineTool : IToolPlugin<SubroutineParameters, SubroutineResults>
    {
        /// <summary>
        /// 无限长直线（Line2d）显示时的单侧截取长度（像素）。
        /// 数学直线无端点，可视化时以直线上一点为中心沿方向向量截取一段可见线段。
        /// </summary>
        private const double DisplayLineHalfLength = 100;

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
            var retainedOwners = new HashSet<ResultResourceOwner>(ReferenceEqualityComparer.Instance);
            void RetainChildren(IEnumerable<ToolResults> children)
            {
                foreach(var owner in children.Select(child=>child.Resources).OfType<ResultResourceOwner>())
                    if(retainedOwners.Add(owner))
                    {
                        var lease=owner.Retain();
                        try{result.RegisterOutputResource(lease);}catch{lease.Dispose();throw;}
                    }
            }

            try
            {
                // ===== 前置验证 =====
                if (string.IsNullOrEmpty(parameters.SubroutineId))
                    throw new InvalidOperationException($"子程序 \"{parameters.SubroutineName}\" 未配置目标工作流");

                if (parameters.ExecuteSubWorkflowAsync == null)
                    throw new InvalidOperationException("子工作流执行委托未注入，请确保通过 WorkflowExecutionEngine 执行包含子程序节点的工作流");

                var validation=parameters.Validate();
                if(!validation.IsValid)throw new InvalidOperationException(string.Join("；",validation.Errors));
                parameters.Context?.CancellationToken.ThrowIfCancellationRequested();
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
                Dictionary<string, object> loopAccumulatedOutputs = null;
                DisplayCollector loopAccumulatedDisplay = null;
                if (parameters.IsLoopEnabled)
                {
                    (execResult, loopAccumulatedOutputs, loopAccumulatedDisplay) =
                        ExecuteWithLoop(parameters, inputImage, contextVariables, resolver, nodeResults, RetainChildren);
                }
                else
                {
                    execResult = ExecuteSubWorkflowOnce(parameters, inputImage, contextVariables);
                }
                using var completedSubRun = execResult;

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

                RetainChildren(subWorkflowNodeResults.Values);
                result.OutputConfigs = parameters.Outputs.ToList();

                // ===== 解析输出项 =====
                // 循环累积模式：累积后的完整输出值（含拍平数组）已由 ExecuteWithLoop 产出，直接采用；
                // 非循环或未勾累积：基于最后轮节点结果解析输出（现状）。
                if (loopAccumulatedOutputs != null)
                {
                    foreach (var kvp in loopAccumulatedOutputs)
                        result.ResolvedOutputValues[kvp.Key] = kvp.Value;
                }
                else
                {
                    ResolveOutputsOnce(result, parameters, subWorkflowNodeResults, resolver);
                }

                // ===== 构建显示项（根据 DisplayConfigs 从子工作流节点结果提取图形）=====
                if (loopAccumulatedDisplay != null)
                {
                    // 循环累积模式：累积显示项图形已由 ExecuteWithLoop 逐轮叠加生成，
                    // 此处只构建非累积显示项（基于最后轮节点结果），再合并累积显示。
                    result.Display.Merge(loopAccumulatedDisplay);
                    BuildDisplayItemsFromConfig(result, parameters, subWorkflowNodeResults, resolver,
                        item => !item.IsAccumulated);
                }
                else
                {
                    BuildDisplayItemsFromConfig(result, parameters, subWorkflowNodeResults, resolver);
                }

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
                    result.SetSuccess(sw.Elapsed.TotalMilliseconds);
                    parameters.LogSuccess($"子程序执行完成: {result.ExecutedNodeCount} 个节点, 耗时 {result.SubWorkflowExecutionTimeMs}ms");
                }
            }
            catch(OperationCanceledException) when(parameters.Context?.CancellationToken.IsCancellationRequested==true)
            {result.Resources?.Dispose();throw;}
            catch (Exception ex)
            {
                sw.Stop();
                parameters.LogError($"子程序异常: {ex.Message}", ex);
                result.SetError($"子程序执行异常: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// 解析单轮子工作流节点结果到输出值（每轮输出项解析），写入 result.ResolvedOutputValues。
        /// 从 Run() 抽出以便循环内逐轮调用；解析失败直接抛异常（rule-008 报错优先）。
        /// </summary>
        private static void ResolveOutputsOnce(
            SubroutineResults result,
            SubroutineParameters parameters,
            Dictionary<string, ToolResults> subWorkflowNodeResults,
            ParameterResolver resolver)
        {
            if (subWorkflowNodeResults.Count == 0)
                return;

            var outputErrors = new List<string>();
            foreach (var output in parameters.Outputs.Where(o => o.Source != null || !string.IsNullOrEmpty(o.BindingSource)))
            {
                string sourceNodeId, sourceProperty;
                // P6/P7：统一从完整绑定路径（NodeId.属性路径，属性路径可含 [Type=X][n].Geometry.X）切分。
                // 优先对象引用 GetBindingPath()（含 BindingSuffix），字符串兜底保留完整路径（不剥离 []），
                // 用 IndexOf('.') 只切第一个点，嵌套路径不被截断。
                var fullPath = output.Source != null ? output.Source.GetBindingPath() : output.BindingSource;
                var dotIndex = fullPath.IndexOf('.');
                if (string.IsNullOrEmpty(fullPath) || dotIndex < 0)
                {
                    outputErrors.Add($"输出 '{output.Name}' BindingSource 格式无效: '{fullPath}'");
                    continue;
                }
                sourceNodeId = fullPath[..dotIndex];
                sourceProperty = fullPath[(dotIndex + 1)..];

                // 目标类型优先取对象引用声明的类型，否则用输出声明的 TypeName
                var targetType = output.Source?.PropertyType
                    ?? SdkTypeVocabulary.ToSystemType(output.TypeName)
                    ?? typeof(object);

                var binding = new ParamSetting
                {
                    ParameterName = output.Name,
                    SourceWorkflowId=output.Source?.SourceWorkflowId,SourceFieldId=output.Source?.SourceFieldId,
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

                // P6：统一从完整绑定路径（NodeId.属性路径）切分。
                // 对象引用 GetBindingPath()（含 BindingSuffix），字符串兜底保留完整路径；
                // IndexOf('.') 只切第一个点，嵌套路径不截断。
                var fullPath = input.Source != null ? input.Source.GetBindingPath() : input.BindingSource;
                var dotIndex = fullPath.IndexOf('.');
                if (string.IsNullOrEmpty(fullPath) || dotIndex < 0)
                {
                    errors.Add($"输入 '{input.Name}' BindingSource 格式无效: '{fullPath}'");
                    continue;
                }
                sourceNodeId = fullPath[..dotIndex];
                sourceProperty = fullPath[(dotIndex + 1)..];

                if (input.Source != null)
                {
                    sourceNodeName = input.Source.SourceNodeName ?? "?";
                    displayName = input.Source.DisplayName ?? sourceProperty;
                }
                else
                {
                    // P0.5-F：不再拿显示名当数据（显示名是命名规则的投影，无可解析性保证）；
                    // 无模型（input.Source == null）时用结构化路径的首段节点 ID 作节点名。
                    sourceNodeName = sourceNodeId;
                    displayName = sourceProperty;
                }

                var bindingKey = input.BindingKey ?? input.Name;
                var binding = new ParamSetting
                {
                    ParameterName = bindingKey,
                    SourceWorkflowId=input.Source?.SourceWorkflowId,SourceFieldId=input.Source?.SourceFieldId,
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

        private static (ExecutionResult ExecResult, Dictionary<string, object> AccumulatedOutputs, DisplayCollector AccumulatedDisplay) ExecuteWithLoop(
            SubroutineParameters parameters, IVisionImage? inputImage,
            Dictionary<string, object> contextVariables, ParameterResolver resolver, IDictionary<string, ToolResults>? nodeResults,
            Action<IEnumerable<ToolResults>> retainChildren)
        {
            var config = parameters.LoopConfig;
            var result = new ExecutionResult();

            int start = ResolveLoopBound(config.StartValueBinding, "循环起始值", resolver, nodeResults);
            int end = ResolveLoopBound(config.EndValueBinding, "循环结束值", resolver, nodeResults);

            parameters.LogInfo($"循环起止: [{start}, {end}), 中断循环: {config.EnableBreak}, 共 {end - start} 次");

            // 循环累积容器：仅在勾选 IsAccumulated 的输出项上生效。
            // 非累积项每轮覆盖（保留最后值，对齐现状），累积项逐轮拍平拼接。
            var accumulatedLists = new Dictionary<string, List<object>>();
            var accumulatedOutputs = new Dictionary<string, object>();

            // 循环内累积显示的最终图形：勾 IsAccumulated 的显示项每轮追加（叠加），
            // 非累积显示项不在此处理（由 Run() 基于最后轮节点结果统一构建）。
            var accumulatedDisplay = new DisplayCollector();

            try
            {
            for (int i = start; i < end; i++)
            {
                parameters.Context?.CancellationToken.ThrowIfCancellationRequested();
                parameters.CurrentIteration = i;
                using var iterResult = ExecuteSubWorkflowOnce(parameters, inputImage, contextVariables);
                result.Merge(iterResult);
                parameters.Context?.CancellationToken.ThrowIfCancellationRequested();

                if (!iterResult.Success)
                    break;

                // ===== 逐轮解析输出与图形（循环核心：真正产生 N 轮数据）=====
                var iterToolResults = ExtractToolResults(iterResult);
                // Earlier iteration images remain borrowed in the accumulated output. Keep only
                // their declared source owners; ordinary numeric accumulation needs no native retain.
                var accumulatedImageSources=parameters.Outputs.Where(output=>output.IsAccumulated&&output.ParamType is { } type&&typeof(IVisionImage).IsAssignableFrom(type.IsArray?type.GetElementType()!:type))
                    .Select(output=>output.Source?.GetBindingPath()??output.BindingSource)
                    .Where(path=>path.Contains('.')).Select(path=>path[..path.IndexOf('.')]).ToHashSet(StringComparer.Ordinal);
                retainChildren(iterToolResults.Where(pair=>accumulatedImageSources.Contains(pair.Key)).Select(pair=>pair.Value));

                // 输出解析：单轮输出值
                var iterResolved = new Dictionary<string, object>();
                if (iterToolResults.Count > 0)
                {
                    var iterResultProxy = new SubroutineResults();
                    ResolveOutputsOnce(iterResultProxy, parameters, iterToolResults, resolver);
                    iterResolved = iterResultProxy.ResolvedOutputValues;
                }

                // 累积显示：仅处理勾选"累积显示"的项，逐轮追加到 accumulatedDisplay（按轮次渐变色）
                BuildAccumulatedDisplay(parameters, iterToolResults, resolver, accumulatedDisplay, i - start, end - start);

                // 累积输出：按 IsAccumulated 逐轮拍平拼接或覆盖最后值
                AccumulateIteration(parameters, iterResolved, accumulatedLists, accumulatedOutputs);

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
                if (config.LoopInterval > 0&&i<end-1)
                    Task.Delay(config.LoopInterval,parameters.Context?.CancellationToken??CancellationToken.None).GetAwaiter().GetResult();
            }

            }
            catch{result.Dispose();throw;}

            return (result, accumulatedOutputs, accumulatedDisplay);
        }

        /// <summary>
        /// 构建累积显示图形：仅处理勾选 IsAccumulated 的显示项，每轮把该轮图形追加到累积 collector。
        /// 按轮次对显示项样式做 HSL 色相渐变（第 0 轮 → 第 N 轮），使多轮叠加的图形能按轨迹区分（对齐 Halcon 多色叠加）。
        /// </summary>
        private static void BuildAccumulatedDisplay(
            SubroutineParameters parameters,
            Dictionary<string, ToolResults> iterToolResults,
            ParameterResolver resolver,
            DisplayCollector accumulatedDisplay,
            int iterationIndex,
            int totalRounds)
        {
            if (parameters.DisplayConfigs.Count == 0 || iterToolResults.Count == 0)
                return;

            foreach (var displayItem in parameters.DisplayConfigs)
            {
                if (!displayItem.IsAccumulated)
                    continue;

                // 绑定源解析（仅累积显示项）
                if (string.IsNullOrEmpty(displayItem.BindingSource) && displayItem.BoundDataSource == null)
                    continue;
                var fullPath = displayItem.BoundDataSource != null
                    ? displayItem.BoundDataSource.GetBindingPath()
                    : displayItem.BindingSource;
                var dotIndex = fullPath.IndexOf('.');
                if (dotIndex < 0)
                {
                    parameters.LogWarning($"显示项 '{displayItem.DisplayName}' 绑定源格式无效: '{fullPath}'");
                    continue;
                }
                string sourceNodeId = fullPath[..dotIndex];
                string sourceProperty = fullPath[(dotIndex + 1)..];

                var binding = new ParamSetting
                {
                    ParameterName = displayItem.DisplayName,
                    SourceWorkflowId = displayItem.BoundDataSource?.SourceWorkflowId,
                    SourceFieldId = displayItem.BoundDataSource?.SourceFieldId,
                    BindingType = BindingType.Binding,
                    SourceNodeId = sourceNodeId,
                    SourceProperty = sourceProperty,
                    TargetType = typeof(object)
                };

                var resolveResult = resolver.Resolve(binding, iterToolResults);
                if (!resolveResult.IsSuccess || resolveResult.Value == null)
                {
                    throw new InvalidOperationException(
                        $"显示项 '{displayItem.DisplayName}' 绑定解析失败: {resolveResult.ErrorMessage ?? "值为空"}");
                }

                // 按轮次渐变色：为当前轮创建带色相偏移的样式副本（不污染原共享样式）
                var iterationStyle = CreateIterationStyle(displayItem.GraphicsStyle, iterationIndex, totalRounds);
                var iterResult = new SubroutineResults();
                AppendDisplayItemWithStyle(iterResult, parameters, displayItem, resolveResult.Value, iterationStyle);
                accumulatedDisplay.Merge(iterResult.Display);
            }
        }

        /// <summary>
        /// 按轮次生成带 HSL 色相渐变的样式副本（复用原样式线宽/透明度等，仅调整 OK 颜色色相）。
        /// 单轮循环（totalRounds<=1）不渐变，保持原样式颜色。
        /// </summary>
        private static GraphicsStyle CreateIterationStyle(GraphicsStyle baseStyle, int iterationIndex, int totalRounds)
        {
            if (baseStyle == null || totalRounds <= 1)
                return baseStyle;

            var clone = new GraphicsStyle
            {
                Name = baseStyle.Name,
                IsVisible = baseStyle.IsVisible,
                NgColor = baseStyle.NgColor,
                Opacity = baseStyle.Opacity,
                LineWidth = baseStyle.LineWidth,
                CrossSize = baseStyle.CrossSize,
            };

            // 以色相为起点，按轮次在 360° 内均布：0→2π*(i/max)
            ToHsv(baseStyle.OkColor, out var baseHue, out var saturation, out var value);
            var hueOffset = totalRounds > 1 ? (double)iterationIndex / totalRounds : 0.0;
            var finalHue = (baseHue + hueOffset) % 1.0;

            clone.OkColor = ColorFromHsv(finalHue * 360.0, saturation, value, baseStyle.OkColor.A);

            return clone;
        }

        /// <summary>RGB → HSV（轮次渐变用，避免依赖 WPF ColorExtensions）</summary>
        private static void ToHsv(Color c, out double hue, out double saturation, out double value)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            value = max;

            if (Math.Abs(delta) < 1e-9)
            {
                hue = 0;
                saturation = 0;
                return;
            }

            saturation = delta / max;

            if (Math.Abs(max - r) < 1e-9)
                hue = (g - b) / delta + (g < b ? 6 : 0);
            else if (Math.Abs(max - g) < 1e-9)
                hue = (b - r) / delta + 2;
            else
                hue = (r - g) / delta + 4;
            hue /= 6.0;
        }

        /// <summary>由 HSV 构造 Color（用于轮次渐变色）</summary>
        private static Color ColorFromHsv(double hue, double saturation, double value, byte alpha)
        {
            hue = ((hue % 360.0) + 360.0) % 360.0;
            double c = value * saturation;
            double x = c * (1 - Math.Abs((hue / 60.0) % 2.0 - 1.0));
            double m = value - c;

            double r, g, b;
            if (hue < 60) { r = c; g = x; b = 0; }
            else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; }
            else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromArgb(alpha,
                (byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }

        /// <summary>
        /// 按指定样式追加显示项（累积渐变色用）。复用 AppendDisplayItem 的分发逻辑，
        /// 但将 displayItem 的样式临时替换为迭代样式，避免修改共享样式实例。
        /// </summary>
        private static void AppendDisplayItemWithStyle(
            SubroutineResults result,
            SubroutineParameters parameters,
            SubroutineDisplayItem displayItem,
            object value,
            GraphicsStyle iterationStyle)
        {
            var originalStyle = displayItem.GraphicsStyle;
            try
            {
                displayItem.GraphicsStyle = iterationStyle;
                AppendDisplayItem(result, parameters, displayItem, value);
            }
            finally
            {
                displayItem.GraphicsStyle = originalStyle;
            }
        }

        /// <summary>
        /// 从 ExecutionResult.NodeResults 提取内部节点 ToolResults 字典（供逐轮输出/图形解析）。
        /// </summary>
        private static Dictionary<string, ToolResults> ExtractToolResults(ExecutionResult execResult)
        {
            var dict = new Dictionary<string, ToolResults>();
            if (execResult.NodeResults != null)
            {
                foreach (var kvp in execResult.NodeResults)
                {
                    if (kvp.Value?.Result != null)
                        dict[kvp.Key] = kvp.Value.Result;
                }
            }
            return dict;
        }

        /// <summary>
        /// 将单轮输出值按累积开关并入累积结果：
        /// - 勾 IsAccumulated 且非 Image 的输出 → 拍平 append 到累积数组（中断时数组长度 = 实际轮数）
        /// - 勾 IsAccumulated 的 Image 输出 → 报错（rule-015 图像不支持累积）
        /// - 未勾 → 覆盖为最后轮值（现状）
        /// </summary>
        private static void AccumulateIteration(
            SubroutineParameters parameters,
            Dictionary<string, object> iterResolved,
            Dictionary<string, List<object>> accumulatedLists,
            Dictionary<string, object> accumulatedOutputs)
        {
            foreach (var output in parameters.Outputs)
            {
                if (!iterResolved.TryGetValue(output.Name, out var value) || value == null)
                    continue;

                if (!output.IsAccumulated)
                {
                    // 非累积：覆盖为最后轮值（现状）
                    accumulatedOutputs[output.Name] = value;
                    continue;
                }

                if (string.Equals(output.TypeName, "Image", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"输出 '{output.Name}' 类型为图像，循环模式下不支持累积输出（rule-015）。请取消该项的\"累计输出\"开关。");
                }

                if (!accumulatedLists.TryGetValue(output.Name, out var list))
                {
                    list = new List<object>();
                    accumulatedLists[output.Name] = list;
                }

                // 单轮值拍平拼接：单轮标量 → append；单轮数组 → 逐元素 append（对齐 Halcon tuple 扁平语义）
                if (value is Array arr)
                {
                    foreach (var item in arr)
                        list.Add(item);
                }
                else
                {
                    list.Add(value);
                }

                // 写入累积数组（拍平后的 T[N*轮]）
                accumulatedOutputs[output.Name] = BuildFlattenedArray(list);
            }
        }

        /// <summary>
        /// 将累积列表按元素类型构建为强类型数组（T[N]）。
        /// </summary>
        private static object BuildFlattenedArray(List<object> items)
        {
            if (items.Count == 0)
                return new object[0];

            var elemType = items[0].GetType();
            var arr = Array.CreateInstance(elemType, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var v = items[i];
                // 数组元素若与首个元素类型不同（少见），统一用 object[] 兜底，避免 InvalidCastException
                if (v == null || v.GetType() != elemType)
                {
                    var objArr = items.Select(x => (object?)x).ToArray();
                    return objArr;
                }
                arr.SetValue(v, i);
            }
            return arr;
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

            // P6：IndexOf('.') 只切第一个点，属性路径含点（嵌套）不被截断
            var dotIndex = bindingPath.IndexOf('.');
            if (dotIndex < 0)
                throw new InvalidOperationException($"{displayName} 绑定源格式无效: '{bindingPath}'，应为 \"节点ID.属性名\"");

            var binding = new ParamSetting
            {
                ParameterName = displayName,
                BindingType = BindingType.Binding,
                SourceNodeId = bindingPath[..dotIndex],
                SourceProperty = bindingPath[(dotIndex + 1)..],
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

            var internalResults=ExtractToolResults(iterResult);
            var type=config.DataType switch{"int"=>typeof(long),"float"=>typeof(double),"string"=>typeof(string),_=>throw new InvalidOperationException("循环比较类型无效。")};
            object Resolve(string path,string name,ImageSourceReference? reference)
            {
                var dot=path.IndexOf('.');if(dot<=0||dot==path.Length-1)throw new InvalidOperationException(name+"未配置有效绑定源。");
                var node=path[..dot];var sources=new Dictionary<string,ToolResults>(StringComparer.Ordinal);
                if(reference?.SourceWorkflowId is {} workflow)
                {
                    var selected=workflow==parameters.SubroutineId?internalResults:nodeResults;
                    if(selected is not null&&selected.TryGetValue(node,out var value))sources.Add(node,value);
                }
                else
                {
                    if(nodeResults?.TryGetValue(node,out var parentValue)==true)sources.Add(node,parentValue);
                    if(internalResults.TryGetValue(node,out var innerValue)&&!sources.TryAdd(node,innerValue))throw new InvalidOperationException("循环比较来源节点身份重复，需要明确流程作用域。");
                }
                try
                {
                    var resolved=resolver.Resolve(new ParamSetting{ParameterName=name,BindingType=BindingType.Binding,SourceNodeId=node,SourceProperty=path[(dot+1)..],SourceWorkflowId=reference?.SourceWorkflowId,SourceFieldId=reference?.SourceFieldId,TargetType=type},sources);
                    if(!resolved.IsSuccess||resolved.Value is null)throw new InvalidOperationException(resolved.ErrorMessage??"值为空。");
                    return resolved.Value;
                }
                catch(Exception ex){throw new InvalidOperationException(name+"解析失败："+ex.Message,ex);}
            }
            var source=Resolve(config.SourceCompareBinding,"源比较值",config.SourceCompareReference);var target=Resolve(config.TargetCompareBinding,"目的比较值",config.TargetCompareReference);
            int comparison;
            if(config.DataType=="string")comparison=StringComparer.Ordinal.Compare((string)source,(string)target);
            else if(config.DataType=="int")comparison=Convert.ToInt64(source,CultureInfo.InvariantCulture).CompareTo(Convert.ToInt64(target,CultureInfo.InvariantCulture));
            else
            {
                var left=Convert.ToDouble(source,CultureInfo.InvariantCulture);var right=Convert.ToDouble(target,CultureInfo.InvariantCulture);
                if(!double.IsFinite(left)||!double.IsFinite(right))throw new InvalidOperationException("循环比较值必须为有限数值。");
                comparison=left.CompareTo(right);
            }
            return config.ComparisonOperator switch{">"=>comparison>0,"<"=>comparison<0,"=="=>comparison==0,"!="=>comparison!=0,">="=>comparison>=0,"<="=>comparison<=0,_=>throw new InvalidOperationException("循环比较运算符无效。")};
        }

        /// <summary>
        /// 根据 DisplayConfigs 从子工作流节点结果中提取图形数据，推入 DisplayCollector。
        /// 对齐输出项解析：优先使用 BoundDataSource（对象引用，含完整类型），BindingSource 字符串兜底。
        /// 分发按解析值的真实类型驱动（而非用户手选的 DisplayType 硬转），圆画真圆、矩形画真矩形。
        /// 不支持的类型直接抛异常（rule-015），不静默跳过。
        /// </summary>
        private static void BuildDisplayItemsFromConfig(
            SubroutineResults result,
            SubroutineParameters parameters,
            Dictionary<string, ToolResults> subWorkflowNodeResults,
            ParameterResolver resolver,
            Func<SubroutineDisplayItem, bool>? filter = null)
        {
            if (parameters.DisplayConfigs.Count == 0)
                return;

            foreach (var displayItem in parameters.DisplayConfigs)
            {
                // 过滤器：仅处理满足条件的显示项（循环累积模式下排除累积项，由 ExecuteWithLoop 单独处理）
                if (filter != null && !filter(displayItem))
                    continue;

                // 绑定源解析：优先对象引用 GetBindingPath()（含 BindingSuffix），字符串兜底保留完整路径。
                // P6/P7：IndexOf('.') 只切第一个点，嵌套路径不截断、整体绑定 [] 不剥离。
                if (string.IsNullOrEmpty(displayItem.BindingSource) && displayItem.BoundDataSource == null)
                    continue;
                var fullPath = displayItem.BoundDataSource != null
                    ? displayItem.BoundDataSource.GetBindingPath()
                    : displayItem.BindingSource;
                var dotIndex = fullPath.IndexOf('.');
                if (dotIndex < 0)
                {
                    parameters.LogWarning($"显示项 '{displayItem.DisplayName}' 绑定源格式无效: '{fullPath}'");
                    continue;
                }
                string sourceNodeId = fullPath[..dotIndex];
                string sourceProperty = fullPath[(dotIndex + 1)..];

                var binding = new ParamSetting
                {
                    ParameterName = displayItem.DisplayName,
                    SourceWorkflowId = displayItem.BoundDataSource?.SourceWorkflowId,
                    SourceFieldId = displayItem.BoundDataSource?.SourceFieldId,
                    BindingType = BindingType.Binding,
                    SourceNodeId = sourceNodeId,
                    SourceProperty = sourceProperty,
                    TargetType = typeof(object)
                };

                var resolveResult = resolver.Resolve(binding, subWorkflowNodeResults);
                if (!resolveResult.IsSuccess || resolveResult.Value == null)
                {
                    throw new InvalidOperationException(
                        $"显示项 '{displayItem.DisplayName}' 绑定解析失败: {resolveResult.ErrorMessage ?? "值为空"}");
                }

                AppendDisplayItem(result, parameters, displayItem, resolveResult.Value);
            }
        }

        /// <summary>
        /// 按解析值的真实类型分发到 DisplayCollector。支持单值与数组两种形态
        /// （内部节点输出契约对齐 Halcon tuple 语义，单值 = 长度 1 数组，解析器会拆箱）。
        /// </summary>
        private static void AppendDisplayItem(
            SubroutineResults result,
            SubroutineParameters parameters,
            SubroutineDisplayItem displayItem,
            object value)
        {
            const bool isPass = true; // 子程序不代为评估内部节点通过/失败，显示属于子程序自身结果
            var style = displayItem.GraphicsStyle;
            var textStyle = displayItem.TextStyle;

            // 文本显示：数字/字符串等标量统一按文本渲染（依赖样式定位）
            if (displayItem.DisplayType == SubroutineDisplayType.Text)
            {
                if (textStyle == null)
                    throw new InvalidOperationException($"显示项 '{displayItem.DisplayName}' 为文本类型但未配置 TextStyle");
                result.Display.AddText(textStyle, textStyle.PositionX, textStyle.PositionY,
                    value.ToString() ?? string.Empty, isPass);
                return;
            }

            // 真实类型分发：优先匹配解析出的运行时类型
            switch (value)
            {
                case Arc2d arc:
                    result.Display.AddArc(style, arc, isPass);
                    break;

                case Circle2d circle:
                    result.Display.AddCircle(style, circle, isPass);
                    break;

                case Arc2d[] arcs:
                    foreach(var arc in arcs)result.Display.AddArc(style, arc, isPass);
                    break;

                case Circle2d[] circles:
                    foreach (var c in circles)
                        result.Display.AddCircle(style, c, isPass);
                    break;

                case Rect rect:
                    result.Display.AddRect(style, rect, isPass);
                    break;

                case Point pt:
                    AppendPoint(result, displayItem, pt, isPass);
                    break;

                case Point2d pt2d:
                    AppendPoint(result, displayItem, new Point(pt2d.X, pt2d.Y), isPass);
                    break;

                case Point2d[] pts2d:
                    if (pts2d.Length == 0)
                        break;
                    if (displayItem.DisplayType == SubroutineDisplayType.Point ||
                        displayItem.DisplayType == SubroutineDisplayType.Cross)
                    {
                        foreach (var p in pts2d)
                            AppendPoint(result, displayItem, new Point(p.X, p.Y), isPass);
                    }
                    else
                    {
                        result.Display.AddContour(style, pts2d.Select(p => new Point(p.X, p.Y)).ToArray(), isPass,
                            displayItem.DisplayType != SubroutineDisplayType.Line);
                    }
                    break;

                case Point[] pts:
                    if (pts.Length == 0)
                        break;
                    if (displayItem.DisplayType == SubroutineDisplayType.Point ||
                        displayItem.DisplayType == SubroutineDisplayType.Cross)
                    {
                        foreach (var p in pts)
                            AppendPoint(result, displayItem, p, isPass);
                    }
                    else
                    {
                        result.Display.AddContour(style, pts, isPass,
                            displayItem.DisplayType != SubroutineDisplayType.Line);
                    }
                    break;

                case Line2d line:
                    result.Display.AddLine(style, line.GetPointAt(-DisplayLineHalfLength), line.GetPointAt(DisplayLineHalfLength), isPass);
                    break;

                case Line2d[] lines:
                    foreach (var l in lines)
                        result.Display.AddLine(style, l.GetPointAt(-DisplayLineHalfLength), l.GetPointAt(DisplayLineHalfLength), isPass);
                    break;

                case RotatedRect2d rr:
                    result.Display.AddContour(style, rr.GetCorners(), isPass, true);
                    break;

                case RotatedRect2d[] rrs:
                    foreach (var r in rrs)
                        result.Display.AddContour(style, r.GetCorners(), isPass, true);
                    break;

                case Polygon2d polygon:
                    result.Display.AddContour(style, polygon.Vertices, isPass, true);
                    break;

                case Polygon2d[] polygons:
                    foreach (var p in polygons)
                        result.Display.AddContour(style, p.Vertices, isPass, true);
                    break;

                // ROI（RoiShape）：Blob 检测区/屏蔽区等图形定义容器。
                // 绑定树按 [Type=X] 下钻后运行期解析出 RoiShape[]，需按其 Geometry 真实类型绘制。
                case RoiShape shape:
                    AppendShapeParameter(result, displayItem, shape, isPass);
                    break;

                case RoiShape[] shapes:
                    foreach (var shape in shapes)
                        AppendShapeParameter(result, displayItem, shape, isPass);
                    break;

                default:
                    throw new NotSupportedException(
                        $"显示项 '{displayItem.DisplayName}' 绑定的值类型 '{value.GetType().Name}' 不支持自动图形映射");
            }
        }

        /// <summary>
        /// 按 RoiShape.Geometry 的真实类型绘制（ROI 图形定义 → DisplayCollector）。
        /// 对齐 Blob 检测区图形的几何类型：Circle2d 画真圆、RotatedRect2d 画四角轮廓、
        /// Polygon2d 画轮廓、Line2d 画线段、Point2d 画点。
        /// （WPF Rect 已从契约层完全退场，故无该分支。）
        /// </summary>
        private static void AppendShapeParameter(
            SubroutineResults result,
            SubroutineDisplayItem displayItem,
            RoiShape sp,
            bool isPass)
        {
            var style = displayItem.GraphicsStyle;
            switch (sp.Geometry)
            {
                case Arc2d arc:
                    result.Display.AddArc(style, arc, isPass);
                    break;

                case Circle2d circle:
                    result.Display.AddCircle(style, circle, isPass);
                    break;

                // P0-2 封口：删除 WPF Rect 分支（几何值恒为契约类型，轴对齐矩形走下面的 RotatedRect2d 分支）。

                case RotatedRect2d rr:
                    result.Display.AddContour(style, rr.GetCorners(), isPass, true);
                    break;

                case Polygon2d poly:
                    result.Display.AddContour(style, poly.Vertices, isPass, true);
                    break;

                case Line2d line:
                    result.Display.AddLine(style, line.GetPointAt(-DisplayLineHalfLength), line.GetPointAt(DisplayLineHalfLength), isPass);
                    break;

                case Point2D pt:
                    AppendPoint(result, displayItem, new System.Windows.Point(pt.X, pt.Y), isPass);
                    break;

                case System.Windows.Point pt:
                    AppendPoint(result, displayItem, pt, isPass);
                    break;

                default:
                    throw new NotSupportedException(
                        $"显示项 '{displayItem.DisplayName}' 绑定的 RoI 图形几何类型 '{sp.Geometry?.GetType().Name ?? "Null"}' 不支持自动图形映射");
            }
        }

        /// <summary>按显示类型追加点（十字 → AddCross，否则 AddPoint）</summary>
        private static void AppendPoint(
            SubroutineResults result,
            SubroutineDisplayItem displayItem,
            Point point,
            bool isPass)
        {
            if (displayItem.DisplayType == SubroutineDisplayType.Cross)
                result.Display.AddCross(displayItem.GraphicsStyle, point, isPass);
            else
                result.Display.AddPoint(displayItem.GraphicsStyle, point, isPass);
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
            Action<SubroutineOutputChange>? onOutputsChanged)
        {
            var control = new SubroutineDebugControl(
                subroutineId, subroutineName, nodeId,
                queryService, subroutineInternalSources,
                parameters, onOutputsChanged);

            // Group五页参数面板按原件中文窗口尺寸组装。
            // 子程序控件不继承 ToolDebugControlBase，DefaultDebugWindow 不会自动读取首选尺寸，此处显式指定。
            try
            {
                var window=new DefaultDebugWindow(control){Owner=owner,Width=1100,Height=680};
                window.Closed+=(_,_)=>control.Dispose();
                return window;
            }
            catch {control.Dispose();throw;}
        }
    }
}
