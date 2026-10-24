using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shell.Controls.Panels;

using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Features.GlobalVariableManager;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Workflow;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Shell.Controls.Shell
{
    /// <summary>
    /// 图像结果区：图像显示 + 模块结果表 + 图像预览。
    /// 从 MainWindow 右侧面板拆分而来，聚焦"结果展示"功能单元。
    /// </summary>
    public partial class ImageResultControl : UserControl
    {
        private MainWindowViewModel _viewModel = null!;

        /// <summary>
        /// 当前选中图像用于本轮；成功完成后才推进到下一张。
        /// </summary>

        public ImageResultControl()
        {
            InitializeComponent();
        }

        /// <summary>将焦点移到现有本地图片预览器，不创建第二份图像列表。</summary>
        public void FocusLocalPreview()
        {
            ImagePreviewContent.BringIntoView();
            ImagePreviewContent.Focus();
            Keyboard.Focus(ImagePreviewContent);
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
            _viewModel?.CloseResultPresentation();
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
        private async void OnImagePreviewExecutionRequested(object sender, WorkflowExecutionRequestEventArgs e)
        {
            if (_viewModel?.WorkflowTabViewModel?.SelectedTab == null)
            {
                _viewModel?.AddLog(LogLevel.Error, "没有选中的工作流，无法执行", LogSource.UIOperation);
                return;
            }
            if (_viewModel.IsRunning)
            {
                // 上一次工作流仍在执行（点图 / 方向键切换过快）：仅同步当前图，不并发执行；
                // 当前 run 结束后的下一次执行会从最新索引开始推进。
                _viewModel.AddLog(LogLevel.Info, $"[REJECTED] 工作流正在运行，已拒绝重复执行请求: {e.ImageInfo.Name}", LogSource.UIOperation);
                return;
            }
            var workflow = _viewModel.WorkflowTabViewModel.SelectedTab;
            // 手动在预览器选中图片（点击/方向键）：以该图为下一轮 run 起点（首轮不自动推进）
            await RunWorkflowWithImage(workflow, e.ImageInfo, e.Index);
        }

        /// <summary>
        /// 从真实模块结果直接绑定全局变量。该入口与“全局变量管理器 → 输入来源”写入同一 ProducerBinding，
        /// 因而不会产生两套依赖配置。
        /// </summary>
        private void BindResultToGlobalVariable_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: FlatResultRow row } ||
                row.BindingInfo == null)
            {
                return;
            }

            var variableType = row.BindingInfo.ValueVocabulary;

            var solutionManager = ServiceInitializer.SolutionManager;
            var solution = solutionManager?.CurrentSolution;
            var tab = _viewModel.WorkflowTabViewModel?.SelectedTab;
            if (solution == null || tab == null)
                return;

            var variable = GlobalVariableSelectDialog.ShowSelect(solutionManager, variableType);
            if (variable == null)
                return;

            var sourceNode = tab.WorkflowNodes.FirstOrDefault(node => node.Id == row.BindingInfo.SourceNodeId);
            variable.ProducerBinding = new ProducerBinding
            {
                WorkflowId = tab.RootWorkflowId,
                WorkflowName = tab.Name,
                NodeId = row.BindingInfo.SourceNodeId,
                NodeName = sourceNode?.DispName ?? row.SourceNodeName ?? row.BindingInfo.SourceNodeId,
                ResultName = row.BindingInfo.ResultPath,
                SourceFieldId=row.BindingInfo.SourceFieldId,
                ResultDisplayName = row.DisplayName
            };
            variable.NotifyInputSourceDisplayChanged();
            SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CommunicationService.OutputBindings.ReconcileAll();
            SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.RefreshCommittedBindings();
            _viewModel.AddLog(LogLevel.Info,
                $"全局变量“{variable.Name}”的输入来源已绑定为 {variable.ProducerBinding.DisplayPath}",
                LogSource.UIOperation);
        }

        /// <summary>以指定图片作为当前输入执行一次工作流（注入运行时参数后触发执行）</summary>
        private async System.Threading.Tasks.Task RunWorkflowWithImage(WorkflowTabViewModel workflow, ImageInfo image, int index)
        {
            // 注入当前图像路径到运行时参数（不设置 ViewModel.CurrentImageIndex）
            _viewModel.PublishRuntimeInput(image.FilePath, index);
            var workflowName = workflow.Name ?? "未知工作流";
            _viewModel.AddLog(LogLevel.Info, $"执行工作流 - 图像: {image.Name}", LogSource.Runtime(workflowName));
            _viewModel.AddLog(LogLevel.Info, $"路径: {image.FilePath}", LogSource.Runtime(workflowName));
            // 触发工作流执行（DisplayImage 由工作流结果驱动更新）
            _viewModel.StatusText = $"运行工作流: {workflow.Name} - {image.Name}";
            await _viewModel.RunSelectedImageAsync();
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
