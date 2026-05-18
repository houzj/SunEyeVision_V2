using System;
using System.Collections.Generic;
using System.Linq;

using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.UI.Services.Performance;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Workflow
{
    public class NodeResultManager
    {
        private readonly WorkflowContext _workflowContext;
        private readonly ViewModels.MainWindowViewModel _viewModel;
        private HalconImageControl? _mainImageControl;
        private HWindow? _mainWindowHandle;
        private readonly ImageConverterPool _imageConverterPool;

        // 双态渲染支持
        private RenderMode _currentRenderMode = RenderMode.Static;
        private DrawingObjectRenderer? _drawingObjectRenderer;

        public NodeResultManager(WorkflowContext workflowContext, ViewModels.MainWindowViewModel viewModel)
        {
            _workflowContext = workflowContext;
            _viewModel = viewModel;
            _imageConverterPool = new ImageConverterPool();
        }

        public void SetMainImageControl(HalconImageControl? imageControl)
        {
            _mainImageControl = imageControl;
            _mainWindowHandle = imageControl?.HalconWindow;
        }

        /// <summary>直接设置 HWindow 句柄（由 MainWindow 在窗口初始化后调用）</summary>
        public void SetMainWindowHandle(HWindow? hWindow)
        {
            _mainWindowHandle = hWindow;
        }

        /// <summary>设置渲染模式（Static/Interactive）</summary>
        public void SetRenderMode(RenderMode mode)
        {
            if (_currentRenderMode == mode) return;

            // Interactive → Static：清理 HDrawingObject
            if (_currentRenderMode == RenderMode.Interactive)
            {
                if (_mainWindowHandle != null)
                {
                    _drawingObjectRenderer?.DetachAll(_mainWindowHandle);
                }
                _drawingObjectRenderer = null;
            }

            _currentRenderMode = mode;
        }

        /// <summary>当前渲染模式</summary>
        public RenderMode CurrentRenderMode => _currentRenderMode;

        /// <summary>
        /// 缓存节点结果 + 更新 ViewModel，不做窗口渲染
        /// 供非选中节点或无 RenderTarget 场景使用
        /// </summary>
        public void CacheNodeResult(WorkflowNodeBase node, ToolResults result)
        {
            if (result == null) return;
            UpdateViewModelResults(result);

            var executionResult = new NodeExecutionResult
            {
                NodeId = node.Id,
                Success = result.IsSuccess,
                ToolResult = result,
            };
            _workflowContext.SetNodeResult(node.Id, executionResult);
        }

        public void UpdateNodeResult(WorkflowNodeBase node, ToolResults result)
        {
            if (result == null) return;
            CacheNodeResult(node, result);

            // 延迟获取 HWindow（如果 HalconImageControl 未初始化或尚未传入 HWindow）
            if (_mainWindowHandle == null && _mainImageControl != null)
                _mainWindowHandle = _mainImageControl.HalconWindow;

            if (_mainWindowHandle == null || !_mainWindowHandle.IsInitialized())
                return;

            // 根据当前选中状态获取正确的显示图像（P1: 优先使用 ComboBox 选中的图像源）
            var displayImage = GetDisplayImageForNode(node, result);

            // 确保在 UI 线程执行渲染（HWindow 和 WPF 控件都要求 UI 线程）
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                RenderToWindow(displayImage, result.Display);
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    RenderToWindow(displayImage, result.Display));
            }

            // 工作流执行完成后触发 AutoFit 策略（首次显示时自动缩放到窗口）
            _mainImageControl?.NotifyImageUpdated();
        }

        /// <summary>
        /// 根据节点的选中状态和用户 ComboBox 选择，确定要显示的图像
        /// </summary>
        private HImage? GetDisplayImageForNode(WorkflowNodeBase node, ToolResults result)
        {
            // 如果该节点是当前选中节点，优先使用用户 ComboBox 选中的图像源
            if (node == _viewModel.SelectedNode)
            {
                var selectedSource = _viewModel.GetSelectedImageSource();
                if (selectedSource != null && selectedSource.SourceNodeId == node.Id)
                {
                    var images = result.GetDisplayImages();
                    if (images.TryGetValue(selectedSource.PropertyName, out var image) &&
                        image != null && image.IsInitialized())
                        return image;
                }
            }

            // 回退：取第一个可用的图像
            return result.GetDisplayImages().Values.FirstOrDefault(m => m != null && m.IsInitialized());
        }

        private void RenderToWindow(HImage? displayImage, DisplayCollector display)
        {
            // 克隆以拥有独立数据（AttachBackgroundToWindow 不拷贝数据，只引用原生内存）
            // 克隆体通过 _mainImageControl.SetImageReference 管理生命周期
            HImage? ownedImage = null;
            if (displayImage != null && displayImage.IsInitialized())
            {
                ownedImage = displayImage.Clone();
            }

            try
            {
                if (_currentRenderMode == RenderMode.Interactive)
                {
                    // ★ Interactive 模式：由 DrawingObjectRenderer 管理 HDrawingObject
                    _drawingObjectRenderer ??= new DrawingObjectRenderer();

                    if (ownedImage != null)
                    {
                        _mainWindowHandle!.AttachBackgroundToWindow(ownedImage);
                        _mainWindowHandle.ClearWindow();
                    }
                    else
                    {
                        _mainWindowHandle?.ClearWindow();
                    }

                    // 渲染 Interactive 图形
                    _drawingObjectRenderer.RenderInteractive(_mainWindowHandle!, display);
                }
                else
                {
                    // ★ Static 模式：走 WindowRenderer（DispObj）
                    WindowRenderer.Render(_mainWindowHandle!, ownedImage, display, RenderMode.Static);
                }

                // 更新 HalconImageControl 的图像引用（传递我们拥有的克隆，不再二次 Clone）
                if (ownedImage != null && _mainImageControl != null)
                {
                    _mainImageControl.SetImageReference(ownedImage);
                }
            }
            catch
            {
                ownedImage?.Dispose();
                throw;
            }
        }

        public void UpdateNodeImageFromCache(WorkflowNodeBase node, object cachedResult)
        {
            // 暂未实现缓存恢复显示
        }

        /// <summary>
        /// 将指定 ToolResult 中某属性图像渲染到主窗口
        /// 仅更换背景层，前景覆盖图形（DispObj/DispText/HDrawingObject）保持不变
        /// 供 ComboBox 图像源切换使用
        /// </summary>
        public void DisplayResultImage(ToolResults result, string propertyName)
        {
            var images = result.GetDisplayImages();
            if (!images.TryGetValue(propertyName, out var image) || image == null || !image.IsInitialized())
                return;

            if (_mainWindowHandle != null && _mainWindowHandle.IsInitialized())
            {
                // 克隆以拥有独立数据，传给窗口背景 + HalconImageControl 管理生命周期
                var ownedImage = image.Clone();
                try
                {
                    _mainWindowHandle.AttachBackgroundToWindow(ownedImage);
                }
                catch
                {
                    ownedImage.Dispose();
                    throw;
                }

                // 更新控件内部引用 + 状态栏（不影响窗口显示内容）
                _mainImageControl?.SetImageReference(ownedImage);
            }
            else
            {
                // 回退到 HalconImageControl 独立显示（DisplayHImage 内部已 Clone，安全）
                _mainImageControl?.DisplayHImage(image);
                return;
            }

            _mainImageControl?.NotifyImageUpdated();
        }

        public void RefreshResultDisplay(WorkflowNodeBase node, ToolResults result)
        {
            UpdateNodeResult(node, result);
        }

        public void SetHighlightItems(System.Collections.Generic.IEnumerable<DisplayItemBase> items)
        {
            // 高亮指定项
        }

        public void ClearHighlight()
        {
            // 清除高亮
        }

        public void ClearResultDisplay()
        {
            _viewModel.CalculationResults.Clear();
            _viewModel.ModuleResultTree.Clear();
            _viewModel.HasModuleResults = false;
            ClearHWindow();
        }

        public void ClearResultGraphics()
        {
            ClearHWindow();
        }

        private void UpdateViewModelResults(ToolResults result)
        {
            _viewModel.CalculationResults.Clear();
            _viewModel.ModuleResultTree.Clear();

            var resultItems = result.GetResultItems();
            foreach (var r in resultItems)
            {
                _viewModel.CalculationResults.Add(new ViewModels.ResultItem
                {
                    Name = r.Name ?? r.DisplayName ?? "",
                    Value = r.Value?.ToString() ?? ""
                });
            }
        }

        private void ClearHWindow()
        {
            if (_mainWindowHandle != null && _mainWindowHandle.IsInitialized())
                _mainWindowHandle.ClearWindow();
            else
                _mainImageControl?.ClearAll();
        }


    }
}
