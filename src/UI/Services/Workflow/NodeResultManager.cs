using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.UI.Extensions;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Services.Performance;
using WorkflowCore = SunEyeVision.Workflow;
using SunEyeVision.Workflow;
using ShapeType = SunEyeVision.Plugin.SDK.UI.Controls.Region.Models.ShapeType;

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
        private readonly WorkflowCore.WorkflowContext _workflowContext;
        private readonly ViewModels.MainWindowViewModel _viewModel;

        // 结果渲染层引用
        private Plugin.SDK.UI.Controls.ResultRenderLayer? _resultRenderLayer;

        // 图像转换器池
        private readonly ImageConverterPool _imageConverterPool;

        public NodeResultManager(WorkflowCore.WorkflowContext workflowContext, ViewModels.MainWindowViewModel viewModel)
        {
            _workflowContext = workflowContext ?? throw new ArgumentNullException(nameof(workflowContext));
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
                ToolResult = result,
                Outputs = new Dictionary<string, object>()
            };

            // 如果结果包含图像，添加到输出
            var outputMat = result.GetOutputImage();
            if (outputMat != null && !outputMat.Empty())
            {
                executionResult.Outputs["Output"] = outputMat;
            }

            _workflowContext.SetNodeResult(node.Id, executionResult);

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
            if (result == null)
            {
                ClearResultDisplay();
                return;
            }

            // ★ Step 1: 获取结果项（虚方法，无反射）
            var resultItems = result.GetResultItems();
            var itemList = resultItems.ToList();

            // ★ Step 2: 批量更新UI
            _viewModel.UpdateCalculationResults(itemList);

            // ★ Step 3: 更新图像（DispImage通道）
            var outputMat = result.GetOutputImage();
            _viewModel.AddLog($"[诊断] 刷新结果显示: 节点={node.Name}, 结果类型={result.GetType().Name}, 输出图像={(outputMat != null ? $"{outputMat.Width}x{outputMat.Height}" : "null")}");

            if (outputMat != null && !outputMat.Empty())
            {
                try
                {
                    var bitmapSource = outputMat.ToBitmapSource();
                    _viewModel.ProcessedImage = bitmapSource;
                    _viewModel.DisplayImage = bitmapSource;

                    var processedType = _viewModel.ImageDisplayTypes
                        .FirstOrDefault(t => t.Type == ViewModels.ImageDisplayType.Processed);
                    if (processedType != null && _viewModel.SelectedImageType?.Type != ViewModels.ImageDisplayType.Processed)
                    {
                        _viewModel.SelectedImageType = processedType;
                    }
                }
                catch
                {
                    // 忽略转换错误
                }
            }

            // ★ Step 4-6 合并：统一构建 DisplayItem 列表（工具输出 + 节点检测区域）
            var displayItems = BuildDisplayItems(result, node);
            _resultRenderLayer?.SetItems(displayItems);
        }

        /// <summary>
        /// 清空结果显示
        /// </summary>
        public void ClearResultDisplay()
        {
            _viewModel.CalculationResults.Clear();
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
            var outputMat = result.GetOutputImage();
            if (outputMat == null || outputMat.Empty())
            {
                return false;
            }

            try
            {
                // 创建唯一ID
                var imageSourceId = $"node_{node.Id}_output_{Guid.NewGuid():N}";

                // 通过ImageSourceManager创建内存图像源
                var imageSource = ImageSourceManager.Instance.CreateFromMemory(
                    outputMat.Clone(), // 克隆Mat，避免所有权问题
                    imageSourceId
                );

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
                    EndTime = DateTime.Now,
                    Outputs = new Dictionary<string, object>()
                };

                if (cachedResult.ProcessedImage != null)
                {
                    var mat = cachedResult.ProcessedImage.ToMat();
                    executionResult.Outputs["Output"] = mat;
                }

                _workflowContext.SetNodeResult(node.Id, executionResult);
            }
            catch
            {
                // 静默处理缓存更新错误
            }
        }

        /// <summary>
        /// 获取节点的输出Mat
        /// </summary>
        public Mat? GetNodeOutputMat(WorkflowNodeBase node)
        {
            if (node == null)
                return null;

            var executionResult = _workflowContext.GetNodeResult(node.Id);
            if (executionResult?.ToolResult == null)
                return null;

            return executionResult.ToolResult.GetOutputImage();
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
        /// 统一构建显示项列表（工具输出 + 节点检测区域）
        /// </summary>
    private IReadOnlyList<DisplayItemBase> BuildDisplayItems(ToolResults result, WorkflowNodeBase node)
            {
                var items = new List<DisplayItemBase>();

                // 1. 工具通过 DisplayCollector 输出的 DisplayItemBase（订阅 Style 变更实现实时联动）
                if (result.Display.HasItems)
                {
                    foreach (var item in result.Display.Items)
                    {
                        // 订阅 Style.PropertyChanged → 联动 IsVisible + 触发重绘
                        SubscribeStyleChanges(item);
                        items.Add(item);
                    }
                }

                // 2. 节点检测区域（只读蓝色显示，使用内部只读 Style）
                if (node.Parameters is RegionParameters regionParams &&
                    regionParams.IsRegionEnabled?.Value == true)
                {
                    var enabledRegions = regionParams.InspectionRegions.GetEnabledRegions();
                    foreach (var region in enabledRegions)
                    {
                        var regionItem = CreateRegionDisplayItem(region);
                        if (regionItem != null) items.Add(regionItem);
                    }
                }

                return items;
            }

        /// <summary>订阅 Style 属性变更 → 联动 DisplayItem.IsVisible 并触发重绘</summary>
        private static void SubscribeStyleChanges(DisplayItemBase item)
        {
            if (item is GraphicsDisplayItem gi && gi.Style != null)
            {
                gi.Style.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(GraphicsStyle.IsVisible))
                        gi.IsVisible = gi.Style.IsVisible;
                    gi.NotifyChanged(); // 触发 RenderLayer 重绘
                };
                gi.IsVisible = gi.Style.IsVisible;
            }
            else if (item is TextDisplayItem ti && ti.Style != null)
            {
                ti.Style.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(TextStyle.IsVisible))
                        ti.IsVisible = ti.Style.IsVisible;
                    ti.NotifyChanged(); // 触发 RenderLayer 重绘
                };
                ti.IsVisible = ti.Style.IsVisible;
            }
        }

        /// <summary>
        /// 根据区域数据创建显示项（只读显示，使用内部 RegionStyle）
        /// </summary>
        private static readonly GraphicsStyle RegionStyle = new()
        {
            Name = "Region",
            OkColor = System.Windows.Media.Color.FromRgb(0, 0, 255),
            LineWidth = 1.5
        };

        private DisplayItemBase? CreateRegionDisplayItem(RegionData region)
            {
                if (region?.Parameters is not ShapeParameters shape) return null;

                return shape.ShapeType switch
                {
                    ShapeType.Rectangle or ShapeType.RotatedRectangle => new GraphicsDisplayItem
                    {
                        Style = RegionStyle, Type = GraphicsType.Rectangle,
                        Geometries = new object[] { new Rect2d(shape.CenterX - shape.Width / 2, shape.CenterY - shape.Height / 2, shape.Width, shape.Height) },
                        ZOrder = -10
                    },
                    ShapeType.Circle => new GraphicsDisplayItem
                    {
                        Style = RegionStyle, Type = GraphicsType.Circle,
                        Geometries = new object[] { new Circle2d(shape.CenterX, shape.CenterY, shape.Radius) },
                        ZOrder = -10
                    },
                    ShapeType.Annulus => new GraphicsDisplayItem
                    {
                        Style = RegionStyle, Type = GraphicsType.Circle,
                        Geometries = new object[]
                        {
                            new Circle2d(shape.CenterX, shape.CenterY, shape.OuterRadius),
                            new Circle2d(shape.CenterX, shape.CenterY, shape.Radius)
                        },
                        IsPassArray = new bool[2], ZOrder = -10
                    },
                    ShapeType.Polygon when shape.Points != null && shape.Points.Count >= 3 => new GraphicsDisplayItem
                    {
                        Style = RegionStyle, Type = GraphicsType.Polygon,
                        Geometries = new object[] { shape.Points.Select(p => new Point2d(p.X, p.Y)).ToArray() },
                        ZOrder = -10
                    },
                    ShapeType.Line => new GraphicsDisplayItem
                    {
                        Style = RegionStyle, Type = GraphicsType.Line,
                        Geometries = new object[] { new LineSegment(new Point2d(shape.StartX, shape.StartY), new Point2d(shape.EndX, shape.EndY)) },
                        ZOrder = -10
                    },
                    _ => null
                };
            }
    }
}
