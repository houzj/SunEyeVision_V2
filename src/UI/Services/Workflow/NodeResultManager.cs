using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Converters;
using SunEyeVision.UI.Services.Performance;
using WorkflowCore = SunEyeVision.Workflow;
using SunEyeVision.Workflow;
using ShapeType = SunEyeVision.Plugin.SDK.Execution.Parameters.ShapeType;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 节点结果管理器 - 负责更新节点结果并刷新UI显示
    /// </summary>
    /// <remarks>
    /// 三通道设计（参考Halcon）：
    /// - DispImage: 图像 → DisplayImage
    /// - DispObj: 几何图形 → ResultRenderLayer.SetShapes()
    /// - DispText: 文本标签 → ResultRenderLayer.SetTexts()
    ///
    /// 使用虚方法替代反射，消除运行时类型检查开销。
    /// </remarks>
    public class NodeResultManager
    {
        private readonly Func<WorkflowCore.WorkflowContext?> _getContext;
        private readonly INodeResultSink _viewModel;

        // 节点第一个图像输出属性名解析（缓存回填时与显示源 key 对齐；null = 不写缓存图像）
        private readonly Func<string, string?>? _getFirstImagePropertyName;

        // 结果渲染层引用
        private Plugin.SDK.UI.Controls.ResultRenderLayer? _resultRenderLayer;

        // 图像转换器池
        private readonly ImageConverterPool _imageConverterPool;

        // 位置修正矩阵诊断收集（字典 → 单行摘要日志，减少假阳性日志干扰）
        private readonly ConcurrentDictionary<string, string> _correctionDiagnostics = new();

        public NodeResultManager(
            Func<WorkflowCore.WorkflowContext?> getContext,
            INodeResultSink viewModel,
            Func<string, string?>? getFirstImagePropertyName = null)
        {
            _getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _getFirstImagePropertyName = getFirstImagePropertyName;
            _imageConverterPool = new ImageConverterPool(maxCacheSize: 10);
        }

        /// <summary>
        /// 设置结果渲染层（由 View 层调用）
        /// </summary>
        public void SetResultRenderLayer(Plugin.SDK.UI.Controls.ResultRenderLayer layer)
        {
            _resultRenderLayer = layer;
        }

        /// <summary>
        /// 更新节点结果并刷新UI
        /// </summary>
        public void UpdateNodeResult(WorkflowNodeBase node, ToolResults result, bool refreshDisplay = true)
        {
            if (node == null || result == null)
                return;

            // 1. 缓存结果到 WorkflowContext
            var executionResult = new NodeExecutionResult
            {
                NodeId = node.Id,
                // ★ 状态取自结果本身，不得硬编码成功（否则失败态被覆盖为成功，状态显示失真）
                Success = result.IsSuccess,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now,
                Result = result
            };

            _getContext()?.SetNodeResult(node.Id, executionResult);

            // 2. 如果当前节点被选中，立即刷新UI（图像显示由 ViewModel 统一解析）
            if (refreshDisplay && node.IsSelected)
            {
                RefreshResultDisplay(node, result);
            }
        }

        /// <summary>
        /// 刷新结果显示
        /// </summary>
        public void RefreshResultDisplay(WorkflowNodeBase node, ToolResults result)
        {
            // ★ 诊断：记录入参
            Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                Plugin.SDK.Logging.LogLevel.Info,
                $"[诊断:RefreshResultDisplay] node.Name='{node.Name}' | node.ToolType='{node.ToolType}' | node.Id='{node.Id}' | result.Type={result?.GetType().Name ?? "null"} | result.IsSelected={node.IsSelected}",
                "NodeResultManager");

            if (result == null)
            {
                ClearResultDisplay();
                return;
            }

            // ★ Step 1-2: 更新模块结果树（分组显示）
            _viewModel.UpdateModuleResults(result, node.Name, node.ToolType, node.Id);

            // ★ Step 3: 更新图像 —— 统一委托 ViewModel 的显示源解析入口
            //   （可用性回退 + 下拉跟随 + 无可用图像时清空，全部收敛在 ViewModel，避免双份实现不一致）
            _viewModel.RefreshDisplayImageFromCurrentSource();

            // ★ Step 4: 结果几何由执行参数快照生成；在调试页应改用当前参数中的显示样式，
            // 以便切换可见性、颜色和线宽时只重绘而不重复执行算法。
            ApplyLiveResultPresentationStyles(node, result);

            // ★ Step 5: 工具输出 → 直接传 DisplayCollector
            _resultRenderLayer?.SetDisplayCollector(result.Display);

            // ★ Step 6: 区域显示
            var regionItems = BuildRegionItems(node);
            _resultRenderLayer?.SetRegionItems(regionItems);
        }

        private static void ApplyLiveResultPresentationStyles(WorkflowNodeBase node, ToolResults result)
        {
            if (node.Parameters == null || result.Display == null) return;

            var presentationProperty = node.Parameters.GetType().GetProperty("ResultPresentation");
            if (presentationProperty?.GetValue(node.Parameters) is ResultPresentationConfig presentation)
                presentation.ApplyLiveStyles(result.Display);
        }

        /// <summary>
        /// 清空结果显示
        /// </summary>
        public void ClearResultDisplay()
        {
            _viewModel.ClearResultDisplay();
            _resultRenderLayer?.Clear();
            _resultRenderLayer?.ClearHighlight();
        }

        /// <summary>
        /// 仅清空图形覆盖层（不清属性树），用于切换到无结果节点时清除残留旧图形
        /// </summary>
        public void ClearResultGraphics()
        {
            _resultRenderLayer?.Clear();
            _resultRenderLayer?.ClearHighlight();
        }

        /// <summary>
        /// 设置高亮元素（用于选中 Blob 等场景的图像高亮反馈）
        /// </summary>
        public void SetHighlightItems(IEnumerable<DisplayItemBase>? items)
        {
            _resultRenderLayer?.SetHighlightItems(items);
        }

        /// <summary>
        /// 清空高亮元素
        /// </summary>
        public void ClearHighlight()
        {
            _resultRenderLayer?.ClearHighlight();
        }

        /// <summary>
        /// 从缓存更新节点图像
        /// </summary>
        public void UpdateNodeImageFromCache(WorkflowNodeBase node, CachedExecutionResult cachedResult)
        {
            if (node == null || cachedResult?.ProcessedImage == null)
                return;

            try
            {
                var executionResult = new NodeExecutionResult
                {
                    NodeId = node.Id,
                    Success = true,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now
                };

                if (cachedResult.ProcessedImage != null)
                {
                    // ★ key 必须与元数据中的图像属性名一致，否则显示取图按 PropertyName 取不到该图
                    var imagePropertyName = _getFirstImagePropertyName?.Invoke(node.Id);
                    if (string.IsNullOrEmpty(imagePropertyName))
                    {
                        Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                            Plugin.SDK.Logging.LogLevel.Warning,
                            $"缓存图像回填跳过：节点 {node.Name}({node.Id}) 无可解析的图像输出属性名",
                            "NodeResultManager");
                        return;
                    }

                    var bmp = cachedResult.ProcessedImage;
                    var stride = bmp.PixelWidth * (bmp.Format.BitsPerPixel / 8);
                    var pixelData = new byte[stride * bmp.PixelHeight];
                    bmp.CopyPixels(pixelData, stride, 0);
                    var image = HalImage.FromBitmapSource(bmp);
                    var genericResults = new GenericToolResults
                    {
                        Status = ExecutionStatus.Success
                    };
                    genericResults.AddResultItem(imagePropertyName, image);
                    executionResult.Result = genericResults;
                }

                _getContext()?.SetNodeResult(node.Id, executionResult);
            }
            catch
            {
                // 静默处理缓存更新错误
            }
        }

        /// <summary>
        /// 获取节点的输出图像
        /// </summary>
        public IVisionImage? GetNodeOutputImage(WorkflowNodeBase node)
        {
            if (node == null)
                return null;

            var executionResult = _getContext()?.GetNodeResult(node.Id);
            if (executionResult?.Result == null)
                return null;

            var images = executionResult.Result.GetDisplayImages();
            return images.Values.FirstOrDefault(m => m != null && !m.IsEmpty);
        }

        /// <summary>
        /// 快速转换Mat到BitmapSource
        /// </summary>
        public BitmapSource? ConvertMatToBitmapSource(Mat? mat)
        {
            if (mat == null || mat.Empty())
                return null;

            try
            {
                return _imageConverterPool.ConvertToBitmapSource(mat);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从工具结果中获取指定属性名的图像并转换为 BitmapSource
        /// </summary>
        public BitmapSource? GetDisplayImageFromSource(ToolResults result, string propertyName)
        {
            var images = result.GetDisplayImages();
            if (images.TryGetValue(propertyName, out var hImage) && hImage != null && !hImage.IsEmpty)
            {
                try
                {
                    return VisionImageToBitmapSourceConverter.ToBitmapSource(hImage);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// 默认检测区样式（当参数中未定义对应样式时使用）
        /// </summary>
        private static readonly GraphicsStyle DefaultInspectionRoiStyle = new()
        {
            Name = "InspectionRoi",
            OkColor = System.Windows.Media.Color.FromRgb(0x44, 0x88, 0xFF),
            LineWidth = 1.5
        };

        /// <summary>
        /// 默认屏蔽区样式（当参数中未定义对应样式时使用）
        /// </summary>
        private static readonly GraphicsStyle DefaultMaskRoiStyle = new()
        {
            Name = "MaskRoi",
            OkColor = System.Windows.Media.Color.FromRgb(255, 165, 0),  // 橙色
            LineWidth = 1.5
        };

        /// <summary>
        /// 从 WorkflowContext 中查询位置修正矩阵。
        /// 通过持久化绑定引用的 SourceNodeId+PropertyName 查询父节点执行结果。
        /// 显示不能依赖调试窗口恢复 AvailableDataSource，也不能依赖其 CurrentValue。
        /// </summary>
        private AffineMatrix? GetPositionCorrectionMatrix(ToolParameters parameters)
        {
            if (parameters == null) return null;

            try
            {
                var isEnabledProp = parameters.GetType().GetProperty("IsPositionCorrectionEnabled");
                if (isEnabledProp == null)
                {
                    _correctionDiagnostics["状态"] = "无IsPositionCorrectionEnabled属性";
                    return null;
                }

                var isEnabledValue = isEnabledProp.GetValue(parameters);
                bool isEnabled = isEnabledValue is ParamValue<bool> pv
                    ? pv.Value
                    : isEnabledValue is true;
                if (!isEnabled)
                {
                    _correctionDiagnostics["状态"] = "位置修正未启用";
                    return null;
                }

                // 工作流只在执行副本中恢复运行时绑定，原节点加载后仍只有持久化引用。
                // 直接读取该引用，使结果绘制与“是否打开过调试窗口”无关。
                var sourceRef = parameters.GetType().GetProperty("PositionCorrectionSource")
                    ?.GetValue(parameters) as ImageSourceReference;
                if (sourceRef == null)
                {
                    var bindingSource = parameters.GetType().GetProperty("PositionCorrectionBindingSource")
                        ?.GetValue(parameters) as AvailableDataSource;
                    sourceRef = ImageSourceReference.FromDataSource(bindingSource);
                }
                if (sourceRef == null || string.IsNullOrEmpty(sourceRef.SourceNodeId) || string.IsNullOrEmpty(sourceRef.PropertyName))
                {
                    _correctionDiagnostics["状态"] = "无有效绑定源";
                    return null;
                }

                _correctionDiagnostics["绑定源"] = $"{sourceRef.SourceNodeId}.{sourceRef.PropertyName}";

                // 直接通过 WorkflowContext 查询父节点执行结果
                var context = _getContext();
                if (context == null)
                {
                    _correctionDiagnostics["状态"] = "无WorkflowContext";
                    return null;
                }

                var parentResult = context.GetNodeResult(sourceRef.SourceNodeId);
                if (parentResult?.Result == null)
                {
                    _correctionDiagnostics["状态"] = $"父节点{sourceRef.SourceNodeId}无执行结果";
                    return null;
                }

                // 兼容上游输出数组（如 ShapeMatch 的 MatchedPositionCorrections）：数组输入取第一个（[0]=最佳）
                var matrix = parentResult.Result.GetOutputValue<AffineMatrix>(sourceRef.PropertyName)
                    ?? (parentResult.Result.GetOutputValue<AffineMatrix[]>(sourceRef.PropertyName) is { Length: > 0 } arr
                        ? arr[0]
                        : null);
                _correctionDiagnostics["矩阵"] = matrix?.ToString() ?? "null";
                _correctionDiagnostics["状态"] = matrix != null ? "已应用" : "父节点输出无矩阵";

                return matrix;
            }
            catch (Exception ex)
            {
                _correctionDiagnostics["状态"] = $"异常:{ex.Message}";
                Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                    Plugin.SDK.Logging.LogLevel.Warning,
                    $"[GetPositionCorrectionMatrix] 查询位置修正矩阵失败: {ex.Message}",
                    "NodeResultManager");
            }

            return null;
        }

        private IEnumerable<DisplayItemBase>? BuildRegionItems(WorkflowNodeBase node)
        {
            if (node.Parameters is not RoiParameters regionParams)
                return null;

            var items = new List<DisplayItemBase>();

            // 获取位置修正矩阵（使结果层与交互层坐标一致）
            var correctionMatrix = GetPositionCorrectionMatrix(node.Parameters);

            // 订阅解析上下文（统一出口：本地/整对象订阅/参数拼装三态归一；无上游结果时返回 null）
            var context = BuildNodeResultsContext();

            // ★ 诊断：结果层矩阵详细信息，与交互层对比
            var matrixHash = correctionMatrix != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(correctionMatrix) : 0;
            var matrixVals = correctionMatrix != null
                ? $"[{correctionMatrix.M11:F4},{correctionMatrix.M12:F4},{correctionMatrix.M21:F4},{correctionMatrix.M22:F4},{correctionMatrix.OffsetX:F2},{correctionMatrix.OffsetY:F2}]"
                : "null";
            Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                Plugin.SDK.Logging.LogLevel.Info,
                $"[结果层] 节点 {node.Name}: 位置修正矩阵={matrixVals} | Hash=0x{matrixHash:X8} | 来源=WorkflowContext",
                "NodeResultManager");

            // 检测区域（蓝色）— 使用参数中的样式实例，UI 的 DispItemColorControl 可绑定控制可见性
            // ★ 组级运行时出口：订阅/拼装组内本地绘制 Roi 不参与显示（按模式过滤），根治"订阅模式双画"。
            var activeRegions = regionParams.InspectionRois.GetRuntimeActiveInputs();
            var inspectionStyle = regionParams.InspectionRoiStyle ?? DefaultInspectionRoiStyle;
            AddRoiDisplayItems(items, activeRegions, inspectionStyle, correctionMatrix, context);

            // 屏蔽区域（橙色）
            var activeMasks = regionParams.MaskRois.GetRuntimeActiveInputs();
            var maskStyle = regionParams.MaskRoiStyle ?? DefaultMaskRoiStyle;
            AddRoiDisplayItems(items, activeMasks, maskStyle, correctionMatrix, context);

            // 测量引导显示（引导线 + 卡尺，与检测/屏蔽区域同通道；测量工具重写 BuildMeasureGuideItems）
            if (regionParams.HasMeasureGuide)
            {
                var guideItems = regionParams.BuildMeasureGuideItems(correctionMatrix);
                if (guideItems != null)
                    items.AddRange(guideItems.Where(i => i != null));
            }

            // 输出位置修正诊断摘要（单行日志，减少假阳性干扰）
            if (_correctionDiagnostics.Count > 0)
            {
                var summary = string.Join(" | ", _correctionDiagnostics.Select(kv => $"{kv.Key}={kv.Value}"));
                Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                    Plugin.SDK.Logging.LogLevel.Info,
                    $"[位置修正] {node.Name}: {summary}",
                    "NodeResultManager");
                _correctionDiagnostics.Clear();
            }

            return items.Count > 0 ? items : null;
        }

        /// <summary>
        /// 构建订阅解析上下文：从 WorkflowContext 汇总全部节点执行结果（仅含已成功结果），
        /// 供区域显示统一出口 <see cref="Roi.ResolveRuntime"/> 解析订阅/拼装区域；无上下文返回 null。
        /// </summary>
        private NodeResultsParameterContext? BuildNodeResultsContext()
        {
            var wf = _getContext();
            if (wf == null) return null;

            var all = wf.GetAllNodeResults();
            if (all == null || all.Count == 0) return null;

            var dict = new Dictionary<string, ToolResults>();
            foreach (var kv in all)
            {
                if (kv.Value?.Result != null)
                    dict[kv.Key] = kv.Value.Result;
            }
            return dict.Count > 0 ? new NodeResultsParameterContext(dict) : null;
        }

        /// <summary>
        /// 区域显示统一循环：每个 ROI 经 SDK 出口 <see cref="Roi.ResolveRuntime"/> 解析出实际几何
        /// （本地/整对象订阅/参数拼装三态归一），逐个生成显示项 + 旋转矩形方向箭头。
        /// 订阅/拼装区域若上游未执行而解析失败：记录日志并跳过该区域（结果层 Fail-Forward，不崩整体显示）。
        /// </summary>
        private static void AddRoiDisplayItems(
            List<DisplayItemBase> items,
            IEnumerable<Roi> rois,
            GraphicsStyle style,
            AffineMatrix? correctionMatrix,
            IParameterContext? context)
        {
            foreach (var roi in rois)
            {
                IReadOnlyList<RoiShape> shapes;
                try
                {
                    shapes = roi.ResolveRuntime(context);
                }
                catch (Exception ex) when (roi.IsSubscribed || roi.IsComposed)
                {
                    Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                        Plugin.SDK.Logging.LogLevel.Warning,
                        $"[结果层] 区域 '{roi.Name}' 解析失败（订阅/拼装需上游节点已执行）: {ex.Message}",
                        "NodeResultManager");
                    continue;
                }

                foreach (var shape in shapes)
                {
                    // ★ 位置修正只作用于本地绘制 Roi（参考系几何 → 当前系）；
                    //   订阅/拼装 Roi 几何来自上游契约输出（已含上游坐标/修正），不再套本节点矩阵（防二次修正）。
                    var matrixForShape = roi.IsLocal ? correctionMatrix : null;
                    var item = CreateRegionDisplayItem(shape, style, matrixForShape);
                    if (item != null) items.Add(item);

                    // 旋转矩形附加方向箭头（完整矩阵跟随，方向随矩阵旋转；颜色跟随区域样式）
                    if (shape.Geometry is RotatedRect2d)
                        items.Add(BuildDirectionArrowItem(shape, style, matrixForShape));
                }
            }
        }

        /// <summary>
        /// 结果层区域显示：按形状有向性应用位置修正（无向剥转 / 有向完整），
        /// 使结果层轮廓与交互层、与算法处理区域空间一致（所见即所得）。
        /// 入参为经统一出口解析出的 RoiShape（不再直读 Roi.LocalGeometry）。
        /// </summary>
        private static DisplayItemBase? CreateRegionDisplayItem(RoiShape shape, GraphicsStyle style, AffineMatrix? correctionMatrix = null)
        {
            if (shape?.Geometry == null) return null;

            // 位置修正按形状有向性解析（对齐 RoiEditorControl 交互层语义）
            var matrix = PositionCorrectionResolver.ResolveForShape(shape.Geometry, correctionMatrix);
            double scale = matrix?.ScaleFactor() ?? 1.0;

            System.Windows.Point TransformPoint(System.Windows.Point p) =>
                matrix != null ? matrix.TransformWpfPoint(p) : p;

            switch (shape.Geometry)
            {
                case Circle2d c:
                    return new GraphicsDisplayItem
                    {
                        Style = style, Type = GraphicsType.Circle,
                        Geometries = new object[] { new Circle2d(TransformPoint(c.Center), c.Radius * scale) }
                    };

                case Annulus2d a:
                    return new GraphicsDisplayItem
                    {
                        Style = style, Type = GraphicsType.Circle,
                        Geometries = new object[]
                        {
                            new Circle2d(TransformPoint(a.Center), a.OuterRadius * scale),
                            new Circle2d(TransformPoint(a.Center), a.InnerRadius * scale)
                        },
                        IsPassArray = new bool[2]
                    };

                case RotatedRect2d rr:
                    return new GraphicsDisplayItem
                    {
                        Style = style, Type = GraphicsType.Polygon,
                        Geometries = new object[]
                        {
                            RotatedRectangleHelper.GetCorners(
                                new System.Windows.Point(rr.CenterX, rr.CenterY), rr.Width, rr.Height, rr.Angle)
                                .Select(TransformPoint).ToArray()
                        }
                    };

                case Polygon2d poly when poly.Vertices.Length >= 3:
                    return new GraphicsDisplayItem
                    {
                        Style = style, Type = GraphicsType.Polygon,
                        Geometries = new object[]
                        {
                            poly.Vertices.Select(v => TransformPoint(new System.Windows.Point(v.X, v.Y))).ToArray()
                        }
                    };

                case System.Windows.Rect r:
                    var center = TransformPoint(new System.Windows.Point(r.X + r.Width / 2, r.Y + r.Height / 2));
                    var displayRect = new System.Windows.Rect(
                        center.X - r.Width * scale / 2, center.Y - r.Height * scale / 2,
                        r.Width * scale, r.Height * scale);
                    return new GraphicsDisplayItem
                    {
                        Style = style, Type = GraphicsType.Rectangle,
                        Geometries = new object[] { displayRect }
                    };

                case Line2d l:
                    return new GraphicsDisplayItem
                    {
                        Style = style, Type = GraphicsType.Line,
                        Geometries = new object[]
                        {
                            new LineSegment(TransformPoint(l.Point), TransformPoint(l.GetPointAt(100)))
                        }
                    };

                default:
                    return null;
            }
        }

        /// <summary>
        /// 构建旋转矩形方向箭头显示项（线宽与轮廓一致，箭杆到矩形边界；端点随位置修正矩阵变换）。
        /// 箭头颜色跟随区域样式（检测区蓝/屏蔽区橙），修复结果层屏蔽区箭头固定蓝色的问题。
        /// </summary>
        private static GraphicsDisplayItem BuildDirectionArrowItem(RoiShape shape, GraphicsStyle regionStyle, AffineMatrix? matrix)
        {
            double arrowLength = 50;
            double angleRad = 0;
            System.Windows.Point centerRef = new(0, 0);
            if (shape.Geometry is RotatedRect2d rr)
            {
                centerRef = new System.Windows.Point(rr.CenterX, rr.CenterY);
                arrowLength = rr.Width / 2;
                angleRad = rr.Angle * Math.PI / 180;
            }
            var endRef = new System.Windows.Point(
                centerRef.X + arrowLength * Math.Cos(angleRad),
                centerRef.Y + arrowLength * Math.Sin(angleRad));
            var center = matrix?.TransformWpfPoint(centerRef) ?? centerRef;
            var end = matrix?.TransformWpfPoint(endRef) ?? endRef;

            // ★ 箭头样式在保持结果层箭头线宽/透明度特性的同时，颜色继承区域角色（检测/屏蔽）
            var arrowStyle = new GraphicsStyle
            {
                Name = ResultArrowStyle.Name,
                OkColor = regionStyle.OkColor,
                NgColor = regionStyle.OkColor,
                LineWidth = ResultArrowStyle.LineWidth,
                Opacity = ResultArrowStyle.Opacity
            };

            return new GraphicsDisplayItem
            {
                Style = arrowStyle,
                Type = GraphicsType.Arrow,
                Geometries = new object[] { new LineSegment(center, end) }
            };
        }

        /// <summary>
        /// 结果显示层旋转矩形方向箭头专用样式（LineWidth=2.0 → 配合 arrowSize=thickness×6，头部=12px）
        /// </summary>
        private static readonly GraphicsStyle ResultArrowStyle = new()
        {
            Name = "ResultArrow",
            OkColor = System.Windows.Media.Color.FromRgb(0, 0, 255),
            NgColor = System.Windows.Media.Color.FromRgb(0, 0, 255),
            LineWidth = 2.0,
            Opacity = 0.7
        };
    }
}
