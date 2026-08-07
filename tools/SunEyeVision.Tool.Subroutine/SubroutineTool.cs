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
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
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

            try
            {
                // ===== 前置验证 =====
                if (string.IsNullOrEmpty(parameters.SubroutineId))
                    throw new InvalidOperationException($"子程序 \"{parameters.SubroutineName}\" 未配置目标工作流");

                if (parameters.ExecuteSubWorkflowAsync == null)
                    throw new InvalidOperationException("子工作流执行委托未注入，请确保通过 WorkflowExecutionEngine 执行包含子程序节点的工作流");

                // 子程序至少需要一个输入和一个输出（rule-015 无静默）
                if (parameters.Inputs.Count == 0)
                    throw new InvalidOperationException($"子程序 \"{parameters.SubroutineName}\" 至少需要一个输入参数");
                if (parameters.Outputs.Count == 0)
                    throw new InvalidOperationException($"子程序 \"{parameters.SubroutineName}\" 至少需要一个输出参数");

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
        /// 对齐输出项解析：优先使用 BoundDataSource（对象引用，含完整类型），BindingSource 字符串兜底。
        /// 分发按解析值的真实类型驱动（而非用户手选的 DisplayType 硬转），圆画真圆、矩形画真矩形。
        /// 不支持的类型直接抛异常（rule-015），不静默跳过。
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
                // 绑定源解析：优先对象引用（含完整类型信息），字符串 "NodeId.PropertyName" 兜底
                string sourceNodeId, sourceProperty;
                if (displayItem.BoundDataSource != null)
                {
                    sourceNodeId = displayItem.BoundDataSource.SourceNodeId;
                    sourceProperty = displayItem.BoundDataSource.PropertyName;
                }
                else
                {
                    if (string.IsNullOrEmpty(displayItem.BindingSource))
                        continue;
                    var parts = displayItem.BindingSource.Split('.');
                    if (parts.Length < 2)
                    {
                        parameters.LogWarning($"显示项 '{displayItem.DisplayName}' 绑定源格式无效: '{displayItem.BindingSource}'");
                        continue;
                    }
                    sourceNodeId = parts[0];
                    sourceProperty = parts[1].EndsWith("[]") ? parts[1][..^2] : parts[1];
                }

                var binding = new ParamSetting
                {
                    ParameterName = displayItem.DisplayName,
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
                case Circle2d circle:
                    result.Display.AddCircle(style, circle, isPass);
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

                // RoI（ShapeParameters）：Blob 检测区/屏蔽区等图形定义容器。
                // 绑定树按 [Type=X] 下钻后运行期解析出 ShapeParameters[]，需按其 Geometry 真实类型绘制。
                case ShapeParameters sp:
                    AppendShapeParameter(result, displayItem, sp, isPass);
                    break;

                case ShapeParameters[] sps:
                    foreach (var sp in sps)
                        AppendShapeParameter(result, displayItem, sp, isPass);
                    break;

                default:
                    throw new NotSupportedException(
                        $"显示项 '{displayItem.DisplayName}' 绑定的值类型 '{value.GetType().Name}' 不支持自动图形映射");
            }
        }

        /// <summary>
        /// 按 ShapeParameters.Geometry 的真实类型绘制（RoI 图形定义 → DisplayCollector）。
        /// 对齐 Blob 检测区图形的几何类型：Circle2d 画真圆、RotatedRect2d 画四角轮廓、
        /// Rect 画矩形、Polygon2d 画轮廓、Line2d 画线段、Point2D 画点。
        /// </summary>
        private static void AppendShapeParameter(
            SubroutineResults result,
            SubroutineDisplayItem displayItem,
            ShapeParameters sp,
            bool isPass)
        {
            var style = displayItem.GraphicsStyle;
            switch (sp.Geometry)
            {
                case Circle2d circle:
                    result.Display.AddCircle(style, circle, isPass);
                    break;

                case System.Windows.Rect rect:
                    result.Display.AddRect(style, rect, isPass);
                    break;

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
