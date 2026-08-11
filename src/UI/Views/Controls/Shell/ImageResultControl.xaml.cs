using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.ViewModels;
using SunEyeVision.UI.Views.Controls.Panels;

namespace SunEyeVision.UI.Views.Controls.Shell
{
    /// <summary>
    /// 图像结果区：图像显示 + 模块结果表 + 图像预览。
    /// 从 MainWindow 右侧面板拆分而来，聚焦"结果展示"功能单元。
    /// </summary>
    public partial class ImageResultControl : UserControl
    {
        private MainWindowViewModel _viewModel = null!;

        public ImageResultControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 注入 ViewModel 并订阅图像预览事件
        /// </summary>
        public void Initialize(MainWindowViewModel viewModel)
        {
            _viewModel = viewModel;
            if (ImagePreviewContent != null)
            {
                ImagePreviewContent.WorkflowExecutionRequested += OnImagePreviewExecutionRequested;
            }
        }

        /// <summary>
        /// 退出时退订图像预览事件
        /// </summary>
        public void Shutdown()
        {
            if (ImagePreviewContent != null)
            {
                ImagePreviewContent.WorkflowExecutionRequested -= OnImagePreviewExecutionRequested;
            }
        }

        /// <summary>
        /// 获取主图像显示控件（供 ViewModel GetMainImageControl 委托注入）
        /// </summary>
        public ImageControl? GetImageDisplayControl() => ImageDisplayContent;

        /// <summary>
        /// 切换图像显示全屏
        /// </summary>
        public void ToggleFullscreen()
        {
            ImageDisplayContent?.ToggleFullscreen();
        }

        /// <summary>
        /// 图像预览请求执行工作流（原 MainWindow.OnWorkflowExecutionRequested）
        /// </summary>
        private void OnImagePreviewExecutionRequested(object sender, WorkflowExecutionRequestEventArgs e)
        {
            if (_viewModel?.WorkflowTabViewModel?.SelectedTab == null)
            {
                _viewModel?.AddLog(LogLevel.Error, "没有选中的工作流，无法执行", LogSource.UIOperation);
                return;
            }
            var workflow = _viewModel.WorkflowTabViewModel.SelectedTab;
            // 注入当前图像路径到运行时参数（不设置 ViewModel.CurrentImageIndex）
            _viewModel.SetRuntimeParameter("CurrentImagePath", e.ImageInfo.FilePath);
            _viewModel.SetRuntimeParameter("CurrentImageIndex", e.Index);
            var workflowName = workflow.Name ?? "未知工作流";
            _viewModel.AddLog(LogLevel.Info, $"执行工作流 - 图像: {e.ImageInfo.Name}", LogSource.Runtime(workflowName));
            _viewModel.AddLog(LogLevel.Info, $"路径: {e.ImageInfo.FilePath}", LogSource.Runtime(workflowName));
            // 触发工作流执行（DisplayImage 由工作流结果驱动更新）
            _viewModel.RunWorkflowCommand.Execute(null);
            _viewModel.StatusText = $"运行工作流: {workflow.Name} - {e.ImageInfo.Name}";
        }

        #region SDK ImageControl 事件处理

        /// <summary>
        /// DataGrid 结果行展开/折叠点击事件
        /// </summary>
        private void ResultExpandIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is FlatResultRow row && row.IsGroup)
            {
                row.IsExpanded = !row.IsExpanded;
                e.Handled = true;
            }
        }

        /// <summary>
        /// 图像加载完成事件
        /// </summary>
        private void OnImageLoaded(object sender, ImageLoadedEventArgs e)
        {
            if (_viewModel != null && e.Image != null)
            {
                _viewModel.StatusText = $"图像加载完成: {e.Width}x{e.Height}";
            }
        }

        /// <summary>
        /// 缩放变化事件
        /// </summary>
        private void OnZoomChanged(object sender, ZoomChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.ImageScale = e.Zoom;
            }
            // 同步视口缩放到结果渲染层（幂律公式：线宽 / 缩放 = 视觉恒定）
            if (ImageDisplayContent?.ResultRenderLayerControl != null)
            {
                ImageDisplayContent.ResultRenderLayerControl.SetViewportScale(e.Zoom);
            }
        }

        #endregion
    }
}
