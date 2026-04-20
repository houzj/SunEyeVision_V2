using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.UI.Extensions;
using SunEyeVision.UI.Models;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Services.Visualization;
using SunEyeVision.UI.Services.Performance;
using SunEyeVision.UI.Views.Controls.Panels;
using WorkflowCore = SunEyeVision.Workflow;
using SunEyeVision.Workflow;

// 别名解决命名冲突
using IOPath = System.IO.Path;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 节点结果管理器 - 负责更新节点结果并刷新UI显示
    /// </summary>
    /// <remarks>
    /// 核心功能:
    /// 1. 缓存工具执行结果到 WorkflowContext（来自 WorkflowCore）
    /// 2. 更新图像数据
    /// 3. 刷新结果面板显示
    /// 4. 显示可视化元素（检测框、轮廓等）
    ///
    /// 使用示例:
    /// <code>
    /// var manager = new NodeResultManager(workflowContext, viewModel);
    /// manager.SetOverlayCanvas(imageControl.OverlayCanvas);
    /// manager.UpdateNodeResult(node, results);
    /// </code>
    /// </remarks>
    public class NodeResultManager
    {
        private readonly WorkflowCore.WorkflowContext _workflowContext;
        private readonly ViewModels.MainWindowViewModel _viewModel;
        private readonly VisualElementRenderer _renderer;

        // 当前显示可视化元素的 Canvas 引用
        private System.Windows.Controls.Canvas? _overlayCanvas;

        // 图像转换器池（用于优化Mat到BitmapSource的转换）
        private readonly ImageConverterPool _imageConverterPool;

        public NodeResultManager(WorkflowCore.WorkflowContext workflowContext, ViewModels.MainWindowViewModel viewModel)
        {
            _workflowContext = workflowContext ?? throw new ArgumentNullException(nameof(workflowContext));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _renderer = new VisualElementRenderer();
            _imageConverterPool = new ImageConverterPool(maxCacheSize: 10);
        }
        
        /// <summary>
        /// 设置叠加层 Canvas（由 View 层调用）
        /// </summary>
        /// <param name="canvas">OverlayCanvas 引用</param>
        public void SetOverlayCanvas(System.Windows.Controls.Canvas canvas)
        {
            _overlayCanvas = canvas;
            System.Diagnostics.Debug.WriteLine($"[NodeResultManager] OverlayCanvas 已设置");
        }

        /// <summary>
        /// 更新节点结果并刷新UI
        /// </summary>
        /// <param name="node">工作流节点</param>
        /// <param name="result">执行结果</param>
        public void UpdateNodeResult(WorkflowNode node, ToolResults result)
        {
            if (node == null || result == null)
                return;

            // 诊断日志已移至Debug输出
            System.Diagnostics.Debug.WriteLine($"[UpdateNodeResult] 节点: {node.Name}, IsSelected={node.IsSelected}");

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
            var outputImage = GetOutputImage(result);
            if (outputImage != null)
            {
                executionResult.Outputs["Output"] = outputImage;
            }

            _workflowContext.SetNodeResult(node.Id, executionResult);

            // 2. 更新图像数据（如果结果包含图像）
            UpdateNodeImage(node, result);

            // 3. 如果当前节点被选中，立即刷新UI
            if (node.IsSelected)
            {
                RefreshResultDisplay(node, result);
            }
        }

        /// <summary>
        /// 刷新结果显示（调用时节点已选中）
        /// </summary>
        /// <remarks>
        /// ★ 防闪烁优化：先准备新数据，再更新UI，避免空白间隙
        /// </remarks>
        public void RefreshResultDisplay(WorkflowNode node, ToolResults result)
        {
            if (result == null)
            {
                ClearResultDisplay();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[RefreshResultDisplay] 开始刷新显示: {node.Name}");

            // ★ Step 1: 先准备所有新数据（不触发UI更新）
            // 使用反射获取所有输出属性（统一使用新机制）
            var propertyMap = result.GetPropertyMap();
            var resultItems = result.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !p.IsDefined(typeof(IgnoreBindAttribute)))
                .Where(p => p.CanRead)
                .Where(p => p.GetMethod?.IsPublic == true)
                .Where(p => p.PropertyType != typeof(Delegate))
                .Where(p => p.Name != "EqualityContract" && !p.Name.StartsWith("get_"))
                .Select(p =>
                {
                    var value = p.GetValue(result);
                    var mapEntry = propertyMap.FirstOrDefault(e => e.PropertyName == p.Name);
                    var displayName = mapEntry?.DisplayName ?? p.Name;
                    var unit = GetDisplayUnit(p.PropertyType);
                    
                    if (value == null) return ResultItem.Text(displayName, "null");
                    if (value is int intVal) return ResultItem.Numeric(displayName, intVal, unit);
                    if (value is double doubleVal) return ResultItem.Numeric(displayName, doubleVal, unit);
                    if (value is bool boolVal) return ResultItem.Boolean(displayName, boolVal);
                    if (value is string stringVal) return ResultItem.Text(displayName, stringVal);
                    if (value is DateTime dateTime) return ResultItem.Text(displayName, dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    if (value is Enum enumVal) return ResultItem.Text(displayName, enumVal.ToString());
                    return ResultItem.Text(displayName, value.ToString() ?? "null");
                })
                .ToList();
            
            // 获取输出图像并转换为 BitmapSource
            var outputImage = GetOutputImage(result);
            BitmapSource? newProcessedImage = null;
            bool hasNewImage = false;
            
            if (outputImage != null)
            {
                try
                {
                    newProcessedImage = outputImage.ToBitmapSource();
                    hasNewImage = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RefreshResultDisplay] 图像转换失败: {ex.Message}");
                }
            }

            // ★ Step 2: 批量更新UI（减少刷新次数）
            _viewModel.UpdateCalculationResults(resultItems);
            
            // ★ Step 3: 设置新图像并显式更新 DisplayImage
            if (hasNewImage && newProcessedImage != null)
            {
                // 设置处理后图像
                _viewModel.ProcessedImage = newProcessedImage;
                
                // ★ 关键修复：显式设置 DisplayImage，不依赖 SelectedImageType setter
                _viewModel.DisplayImage = newProcessedImage;
                
                // 确保显示类型为 Processed（如果当前不是）
                var processedType = _viewModel.ImageDisplayTypes
                    .FirstOrDefault(t => t.Type == ViewModels.ImageDisplayType.Processed);
                if (processedType != null && _viewModel.SelectedImageType?.Type != ViewModels.ImageDisplayType.Processed)
                {
                    _viewModel.SelectedImageType = processedType;
                }
            }
            
            // ★ Step 4: 更新可视化元素
            UpdateVisualElements(result);
        }

        /// <summary>
        /// 清空结果显示
        /// </summary>
        /// <remarks>
        /// ★ 防闪烁优化：不再主动清空图像，由调用方控制
        /// </remarks>
        public void ClearResultDisplay()
        {
            _viewModel.CalculationResults.Clear();
            ClearVisualElements();
            
            // ★ 优化：不再清空图像，保留上一个图像直到新图像到来
            // 这样可以避免节点切换时的闪烁
            // 图像清空由 UpdateDisplayImageSources 或特定逻辑控制
        }
        
        /// <summary>
        /// 更新可视化元素显示
        /// </summary>
        /// <param name="result">工具执行结果</param>
        private void UpdateVisualElements(ToolResults result)
        {
            if (_overlayCanvas == null)
            {
                System.Diagnostics.Debug.WriteLine("[NodeResultManager] OverlayCanvas 未设置，无法显示可视化元素");
                return;
            }
            
            // 清除旧元素
            ClearVisualElements();
            
            // 获取可视化元素
            var visualElements = result.GetVisualElements();
            if (visualElements == null)
            {
                System.Diagnostics.Debug.WriteLine("[NodeResultManager] 结果不包含可视化元素");
                return;
            }
            
            var elementList = visualElements.ToList();
            if (elementList.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[NodeResultManager] 可视化元素列表为空");
                return;
            }
            
            // 渲染并添加到 Canvas
            var uiElements = _renderer.Render(elementList);
            foreach (var uiElement in uiElements)
            {
                _overlayCanvas.Children.Add(uiElement);
            }
            
            System.Diagnostics.Debug.WriteLine($"[NodeResultManager] 显示 {uiElements.Count} 个可视化元素");
        }
        
        /// <summary>
        /// 清除可视化元素
        /// </summary>
        private void ClearVisualElements()
        {
            _overlayCanvas?.Children.Clear();
        }

        /// <summary>
        /// 更新节点图像数据
        /// </summary>
        /// <remarks>
        /// 重构说明：
        /// - 新架构：更新 OutputCache（输出缓存），不修改 InputSource（输入源）
        /// - ImageSource集成：创建MemoryImageSource并注册到ImageSourceManager
        /// - 向后兼容：同时更新 ImageData（已过时）
        /// - 设计原则：输入和输出分离，执行结果不应修改用户选择的输入数据
        /// </remarks>
        public bool UpdateNodeImage(WorkflowNode node, ToolResults result)
        {
            // 尝试从结果中获取输出图像
            var outputImage = GetOutputImage(result);
            if (outputImage == null || outputImage.Empty())
            {
                // ★ 关键日志：无图像输出
                System.Diagnostics.Debug.WriteLine($"[NodeResultManager] 节点 {node.Name} 无图像输出");
                return false;
            }

            // 获取文件路径（如果有）
            var filePathProp = result.GetType().GetProperty("FilePath");
            var filePath = filePathProp?.GetValue(result) as string ?? "";

            try
            {
                // 转换为 BitmapSource
                var bitmapSource = outputImage.ToBitmapSource();

                // 创建唯一ID
                var imageSourceId = $"node_{node.Id}_output_{Guid.NewGuid():N}";

                // 通过ImageSourceManager创建内存图像源
                var imageSource = ImageSourceManager.Instance.CreateFromMemory(
                    outputImage.Clone(), // 克隆Mat，避免所有权问题
                    imageSourceId
                );

                System.Diagnostics.Debug.WriteLine($"[NodeResultManager] ★ ImageSource已创建: {imageSourceId}");

                // ★ 关键日志：图像更新成功
                System.Diagnostics.Debug.WriteLine($"[NodeResultManager] ★ 节点 {node.Name} 图像已更新");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NodeResultManager] 更新节点图像失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从结果中获取输出图像
        /// </summary>
        private Mat? GetOutputImage(ToolResults result)
        {
            // 检查结果是否包含图像数据
            if (result == null)
                return null;

            // 尝试通过反射获取 OutputImage 属性
            var outputImageProp = result.GetType().GetProperty("OutputImage");
            if (outputImageProp != null)
            {
                var value = outputImageProp.GetValue(result);
                if (value is Mat mat)
                {
                    return mat;
                }
            }

            return null;
        }

        /// <summary>
        /// 从缓存更新节点图像（优化版本）
        /// </summary>
        /// <param name="node">工作流节点</param>
        /// <param name="cachedResult">缓存的执行结果</param>
        public void UpdateNodeImageFromCache(WorkflowNode node, CachedExecutionResult cachedResult)
        {
            if (node == null || cachedResult == null || cachedResult.ProcessedImage == null)
                return;

            try
            {
                // 直接使用缓存的BitmapSource，无需重新转换
                var bitmapSource = cachedResult.ProcessedImage;

                // 将缓存的执行结果存储到 WorkflowContext
                var executionResult = new NodeExecutionResult
                {
                    NodeId = node.Id,
                    Success = true,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now,
                    Outputs = new Dictionary<string, object>()
                };

                // 如果有缓存的图像，添加到执行结果
                if (cachedResult.ProcessedImage != null)
                {
                    // 将 BitmapSource 转换为 Mat
                    var mat = cachedResult.ProcessedImage.ToMat();
                    executionResult.Outputs["Output"] = mat;
                }

                _workflowContext.SetNodeResult(node.Id, executionResult);

                System.Diagnostics.Debug.WriteLine(
                    $"[NodeResultManager] ★ 缓存更新: 节点 {node.Name}, " +
                    $"执行时间: {cachedResult.ExecutionTimeMs}ms, " +
                    $"缓存命中");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NodeResultManager] 缓存更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取节点的输出Mat（用于缓存）
        /// </summary>
        /// <param name="node">工作流节点</param>
        /// <returns>输出Mat，如果无输出返回null</returns>
        public Mat? GetNodeOutputMat(WorkflowNode node)
        {
            if (node == null)
                return null;

            var executionResult = _workflowContext.GetNodeResult(node.Id);
            if (executionResult?.ToolResult == null)
                return null;

            return GetOutputImage(executionResult.ToolResult);
        }

        /// <summary>
        /// 快速转换Mat到BitmapSource（使用图像转换池优化）
        /// </summary>
        /// <param name="mat">OpenCV Mat</param>
        /// <returns>BitmapSource</returns>
        public BitmapSource? ConvertMatToBitmapSource(Mat? mat)
        {
            if (mat == null || mat.Empty())
                return null;

            try
            {
                return _imageConverterPool.ConvertToBitmapSource(mat);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NodeResultManager] 图像转换失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取属性类型的显示单位
        /// </summary>
        /// <param name="propertyType">属性类型</param>
        /// <returns>显示单位字符串</returns>
        private static string GetDisplayUnit(Type propertyType)
        {
            if (propertyType == typeof(OpenCvSharp.Size) || 
                propertyType == typeof(OpenCvSharp.Rect) ||
                propertyType == typeof(OpenCvSharp.Point))
                return "像素";
            return string.Empty;
        }
    }
}
