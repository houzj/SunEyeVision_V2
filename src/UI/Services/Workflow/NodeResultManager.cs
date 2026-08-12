using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Rendering;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Converters;
using SunEyeVision.UI.Services.Performance;
using WorkflowCore = SunEyeVision.Workflow;
using SunEyeVision.Workflow;
using ShapeType = SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeType;

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

        // 结果渲染层引用
        private Plugin.SDK.UI.Controls.ResultRenderLayer? _resultRenderLayer;

        // 图像转换器池
        private readonly ImageConverterPool _imageConverterPool;

        // 位置修正矩阵诊断收集（字典 → 单行摘要日志，减少假阳性日志干扰）
        private readonly ConcurrentDictionary<string, string> _correctionDiagnostics = new();

        public NodeResultManager(Func<WorkflowCore.WorkflowContext?> getContext, INodeResultSink viewModel)
        {
            _getContext = getContext ?? throw new ArgumentNullException(nameof(getContext));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
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
        public void UpdateNodeResult(WorkflowNodeBase node, ToolResults result)
        {
            if (node == null || result == null)
                return;

            // 1. 缓存结果到 WorkflowContext
            var executionResult = new NodeExecutionResult
            {
                NodeId = node.Id,
                Success = true,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now,
                Result = result
            };

            _getContext()?.SetNodeResult(node.Id, executionResult);

            // 2. 更新图像数据
            UpdateNodeImage(node, result);

            // 3. 如果当前节点被选中，立即刷新UI
            if (node.IsSelected)
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

            // ★ Step 3: 更新图像 - 基于当前 ComboBox 选中项（跨节点支持）
            var selectedSource = _viewModel.GetSelectedImageSource();
            if (selectedSource != null)
            {
                // 根据 SourceNodeId 获取源节点的结果对象，支持显示父节点的图像
                ToolResults sourceResult;
                if (selectedSource.SourceNodeId == node.Id)
                {
                    sourceResult = result; // 本节点：直接用参数
                }
                else
                {
                    // 父节点/其他节点：从 WorkflowContext 缓存获取
                    var sourceExecResult = _getContext()?.GetNodeResult(selectedSource.SourceNodeId);
                    sourceResult = sourceExecResult?.Result;
                    if (sourceResult == null)
                        return; // 尚未执行的节点跳过，不清空当前图像
                }

                var bitmapSource = GetDisplayImageFromSource(sourceResult, selectedSource.PropertyName);
                if (bitmapSource != null)
                {
                    _viewModel.SetDisplayImageFromBitmap(bitmapSource);
                }
                else
                {
                    // 节点无可用显示图像 → 清除残留图像
                    _viewModel.DisplayImage = null;
                }
            }
            else
            {
                // 无可选图像源 → 清除残留图像
                _viewModel.DisplayImage = null;
            }

            // ★ Step 4: 工具输出 → 直接传 DisplayCollector
            _resultRenderLayer?.SetDisplayCollector(result.Display);

            // ★ Step 5: 区域显示
            var regionItems = BuildRegionItems(node);
            _resultRenderLayer?.SetRegionItems(regionItems);
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
        /// 更新节点图像数据
        /// </summary>
        public bool UpdateNodeImage(WorkflowNodeBase node, ToolResults result)
        {
            var images = result.GetDisplayImages();
            var primaryImage = images.Values.FirstOrDefault(m => m != null && !m.IsEmpty);
            if (primaryImage == null)
            {
                return false;
            }

            try
            {
                // 直接缓存 IVisionImage 到 WorkflowContext
                var nodeResult = _getContext()?.GetNodeResult(node.Id);
                if (nodeResult == null)
                {
                    nodeResult = new NodeExecutionResult
                    {
                        NodeId = node.Id,
                        Success = true,
                        StartTime = DateTime.Now,
                        EndTime = DateTime.Now
                    };
                    _getContext()?.SetNodeResult(node.Id, nodeResult);
                }

                return true;
            }
            catch
            {
                return false;
            }
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
                    var bmp = cachedResult.ProcessedImage;
                    var stride = bmp.PixelWidth * (bmp.Format.BitsPerPixel / 8);
                    var pixelData = new byte[stride * bmp.PixelHeight];
                    bmp.CopyPixels(pixelData, stride, 0);
                    var image = VisionImage.FromBitmapSource(bmp);
                    var genericResults = new GenericToolResults
                    {
                        Status = ExecutionStatus.Success
                    };
                    genericResults.AddResultItem("Output", image);
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
        /// 默认检测区域样式（当参数中未定义对应样式时使用）
        /// </summary>
        private static readonly GraphicsStyle DefaultRegionStyle = new()
        {
            Name = "Region",
            OkColor = System.Windows.Media.Color.FromRgb(0x44, 0x88, 0xFF),
            LineWidth = 1.5
        };

        /// <summary>
        /// 默认屏蔽区域样式（当参数中未定义对应样式时使用）
        /// </summary>
        private static readonly GraphicsStyle DefaultMaskRegionStyle = new()
        {
            Name = "MaskRegion",
            OkColor = System.Windows.Media.Color.FromRgb(255, 165, 0),  // 橙色
            LineWidth = 1.5
        };

        /// <summary>
        /// 从 WorkflowContext 中查询位置修正矩阵。
        /// 直接通过 AvailableDataSource 的 SourceNodeId+PropertyName 查询父节点执行结果，
        /// 避免依赖 CurrentValue（设计时为 null，仅执行时临时填充）。
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

                // 获取 AvailableDataSource 信息（SourceNodeId + PropertyName）
                var bindingSourceProp = parameters.GetType().GetProperty("PositionCorrectionBindingSource");
                var dataSource = bindingSourceProp?.GetValue(parameters) as AvailableDataSource;
                if (dataSource == null || string.IsNullOrEmpty(dataSource.SourceNodeId) || string.IsNullOrEmpty(dataSource.PropertyName))
                {
                    _correctionDiagnostics["状态"] = "无有效绑定源";
                    return null;
                }

                _correctionDiagnostics["绑定源"] = $"{dataSource.SourceNodeId}.{dataSource.PropertyName}";

                // 直接通过 WorkflowContext 查询父节点执行结果
                var context = _getContext();
                if (context == null)
                {
                    _correctionDiagnostics["状态"] = "无WorkflowContext";
                    return null;
                }

                var parentResult = context.GetNodeResult(dataSource.SourceNodeId);
                if (parentResult?.Result == null)
                {
                    _correctionDiagnostics["状态"] = $"父节点{dataSource.SourceNodeId}无执行结果";
                    return null;
                }

                var matrix = parentResult.Result.GetOutputValue<AffineMatrix>(dataSource.PropertyName);
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
            var enabledRegions = regionParams.InspectionRegions.GetEnabledRois();
            var inspectionStyle = regionParams.InspectionRegionStyle ?? DefaultRegionStyle;
            foreach (var region in enabledRegions)
            {
                var regionItem = CreateRegionDisplayItem(region, inspectionStyle, correctionMatrix);
                if (regionItem != null) items.Add(regionItem);

                // 旋转矩形附加方向箭头
                if (region.Parameters is ShapeParameters rShape && rShape.Geometry is RotatedRect2d)
                {
                    var arrowCenter = correctionMatrix != null
                        ? correctionMatrix.TransformWpfPoint(new System.Windows.Point(((RotatedRect2d)rShape.Geometry).CenterX, ((RotatedRect2d)rShape.Geometry).CenterY))
                        : new System.Windows.Point(((RotatedRect2d)rShape.Geometry).CenterX, ((RotatedRect2d)rShape.Geometry).CenterY);

                    items.Add(BuildDirectionArrowItem(rShape, arrowCenter));
                }
            }

            // 屏蔽区域（橙色）
            var enabledMasks = regionParams.MaskRegions.GetEnabledRois();
            var maskStyle = regionParams.MaskRegionStyle ?? DefaultMaskRegionStyle;
            foreach (var region in enabledMasks)
            {
                var regionItem = CreateRegionDisplayItem(region, maskStyle, correctionMatrix);
                if (regionItem != null) items.Add(regionItem);

                // 旋转矩形附加方向箭头
                if (region.Parameters is ShapeParameters mShape && mShape.Geometry is RotatedRect2d)
                {
                    var arrowCenter = correctionMatrix != null
                        ? correctionMatrix.TransformWpfPoint(new System.Windows.Point(((RotatedRect2d)mShape.Geometry).CenterX, ((RotatedRect2d)mShape.Geometry).CenterY))
                        : new System.Windows.Point(((RotatedRect2d)mShape.Geometry).CenterX, ((RotatedRect2d)mShape.Geometry).CenterY);

                    items.Add(BuildDirectionArrowItem(mShape, arrowCenter));
                }
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

        private static DisplayItemBase? CreateRegionDisplayItem(RoiData region, GraphicsStyle style, AffineMatrix? correctionMatrix = null)
        {
            if (region?.Parameters is not ShapeParameters shape) return null;

            System.Windows.Point GetCorrectionPoint(double x, double y) =>
                correctionMatrix != null
                    ? correctionMatrix.TransformWpfPoint(new System.Windows.Point(x, y))
                    : new System.Windows.Point(x, y);

            System.Windows.Point center = new(0, 0);
            System.Windows.Point lineStart = new(0, 0);
            System.Windows.Point lineEnd = new(0, 0);
            List<System.Windows.Point>? polygonPoints = null;

            if (shape.Geometry != null)
            {
                if (shape.Geometry is Circle2d c)
                    center = GetCorrectionPoint(c.Center.X, c.Center.Y);
                else if (shape.Geometry is RotatedRect2d rr)
                    center = GetCorrectionPoint(rr.CenterX, rr.CenterY);
                else if (shape.Geometry is Line2d l)
                {
                    lineStart = GetCorrectionPoint(l.Point.X, l.Point.Y);
                    lineEnd = GetCorrectionPoint(l.GetPointAt(100).X, l.GetPointAt(100).Y);
                }
                else if (shape.Geometry is System.Windows.Rect r)
                    center = GetCorrectionPoint(r.X + r.Width / 2, r.Y + r.Height / 2);
                else if (shape.Geometry is Polygon2d poly)
                    polygonPoints = poly.Vertices.Select(v => GetCorrectionPoint(v.X, v.Y)).ToList();
            }

            if (shape.Geometry == null) return null;

            if (shape.Geometry is Circle2d circleVal)
            {
                return new GraphicsDisplayItem
                {
                    Style = style, Type = GraphicsType.Circle,
                    Geometries = new object[] { new Circle2d(circleVal.Center.X, circleVal.Center.Y, circleVal.Radius) }
                };
            }
            else if (shape.Geometry is RotatedRect2d rr)
            {
                return new GraphicsDisplayItem
                {
                    Style = style, Type = GraphicsType.Polygon,
                    Geometries = new object[]
                    {
                        RotatedRectangleHelper.GetCorners(
                            new System.Windows.Point(rr.CenterX, rr.CenterY), rr.Width, rr.Height, rr.Angle)
                    }
                };
            }
            else if (shape.Geometry is Annulus2d a)
            {
                return new GraphicsDisplayItem
                {
                    Style = style, Type = GraphicsType.Circle,
                    Geometries = new object[]
                    {
                        new Circle2d(a.Center.X, a.Center.Y, a.OuterRadius),
                        new Circle2d(a.Center.X, a.Center.Y, a.InnerRadius)
                    },
                    IsPassArray = new bool[2]
                };
            }
            else if (shape.Geometry is Polygon2d poly && poly.Vertices.Length >= 3)
            {
                return new GraphicsDisplayItem
                {
                    Style = style, Type = GraphicsType.Polygon,
                    Geometries = new object[] { poly.Vertices.Select(v => new System.Windows.Point(v.X, v.Y)).ToArray() }
                };
            }
            else if (shape.Geometry is System.Windows.Rect r)
            {
                return new GraphicsDisplayItem
                {
                    Style = style, Type = GraphicsType.Rectangle,
                    Geometries = new object[] { r }
                };
            }
            else if (shape.Geometry is Line2d l)
            {
                return new GraphicsDisplayItem
                {
                    Style = style, Type = GraphicsType.Line,
                    Geometries = new object[] { new LineSegment(l.Point, l.GetPointAt(100)) }
                };
            }

            return null;
        }

        /// <summary>
        /// 结果显示层旋转矩形轮廓样式（与交互层 RegionEditor 一致）
        /// </summary>
        private static readonly GraphicsStyle ResultRotatedRegionStyle = new()
        {
            Name = "ResultRotatedRegion",
            OkColor = System.Windows.Media.Color.FromRgb(0, 0, 255),
            NgColor = System.Windows.Media.Color.FromRgb(0, 0, 255),
            LineWidth = 2.0,
            Opacity = 0.7
        };

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

        /// <summary>
        /// 构建旋转矩形方向箭头显示项（线宽与轮廓一致，箭杆到矩形边界）
        /// </summary>
        private static GraphicsDisplayItem BuildDirectionArrowItem(ShapeParameters shape, System.Windows.Point center)
        {
            double arrowLength = 50;
            double angleRad = 0;
            if (shape.Geometry is RotatedRect2d rr)
            {
                arrowLength = rr.Width / 2;
                angleRad = rr.Angle * Math.PI / 180;
            }
            var arrowEnd = new System.Windows.Point(
                center.X + arrowLength * Math.Cos(angleRad),
                center.Y + arrowLength * Math.Sin(angleRad));

            return new GraphicsDisplayItem
            {
                Style = ResultArrowStyle,
                Type = GraphicsType.Arrow,
                Geometries = new object[] { new LineSegment(center, arrowEnd) }
            };
        }
    }
}
