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

        public void UpdateNodeResult(WorkflowNodeBase node, ToolResults result)
        {
            if (result == null) return;
            UpdateViewModelResults(result);

            // 延迟获取 HWindow（如果 HalconImageControl 未初始化或尚未传入 HWindow）
            if (_mainWindowHandle == null && _mainImageControl != null)
                _mainWindowHandle = _mainImageControl.HalconWindow;

            var displayImage = result.GetDisplayImages().Values.FirstOrDefault(m => m != null && m.IsInitialized());

            if (_mainWindowHandle == null || !_mainWindowHandle.IsInitialized())
                return;

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
        }

        private void RenderToWindow(HImage? displayImage, DisplayCollector display)
        {
            if (_currentRenderMode == RenderMode.Interactive)
            {
                // ★ Interactive 模式：由 DrawingObjectRenderer 管理 HDrawingObject
                _drawingObjectRenderer ??= new DrawingObjectRenderer();

                // 先清除窗口 + 设置背景图像
                if (displayImage != null && displayImage.IsInitialized())
                {
                    _mainWindowHandle!.AttachBackgroundToWindow(displayImage);
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
                WindowRenderer.Render(_mainWindowHandle!, displayImage, display, RenderMode.Static);
            }

            if (displayImage != null && _mainImageControl != null)
            {
                _mainImageControl.SetImageReference(displayImage);
                _mainImageControl.FitToWindow();
            }
        }

        public void UpdateNodeImageFromCache(WorkflowNodeBase node, object cachedResult)
        {
            // 暂未实现缓存恢复显示
        }

        /// <summary>
        /// 将指定 ToolResult 中某属性图像+覆盖图形渲染到主窗口
        /// </summary>
        public void DisplayResultImage(ToolResults result, string propertyName)
        {
            var images = result.GetDisplayImages();
            if (!images.TryGetValue(propertyName, out var image) || image == null || !image.IsInitialized())
                return;

            if (_mainWindowHandle != null && _mainWindowHandle.IsInitialized())
            {
                RenderToWindow(image, result.Display);
            }
            else
            {
                _mainImageControl?.SetImageReference(image);
                _mainImageControl?.FitToWindow();
            }
        }

        public void RefreshResultDisplay(WorkflowNodeBase node, ToolResults result)
        {
            // 刷新当前节点结果显示
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
