using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Shared;
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
        private SunEyeVision.UI.Services.Images.ImageSourceStripSession? _sourceSession;
        private WorkflowTabViewModel? _sourceWorkflow;
        private readonly SunEyeVision.UI.Services.Images.ImageSourceThemeScope _sourceTheme;
        private readonly SunEyeVision.UI.Services.Monitoring.ResultViewThemeScope _resultTheme;
        private void ReleaseSourceSession()
        {ImagePreviewContent.Session=null;_sourceSession?.Dispose();_sourceSession=null;_sourceWorkflow=null;}
        private void RefreshSourceSession()
        {
            if(!IsLoaded||_viewModel is null){ReleaseSourceSession();return;}
            var main=_viewModel;var source=main.ActiveInputSource;var workflow=main.WorkflowTabViewModel.SelectedTab;
            if(ReferenceEquals(_sourceSession?.Source,source)&&ReferenceEquals(_sourceWorkflow,workflow))return;
            ReleaseSourceSession();
            if(source is null||workflow is null)return;
            _sourceWorkflow=workflow;
            _sourceSession=new(source,()=>IsLoaded&&ReferenceEquals(_viewModel,main)
                &&ReferenceEquals(main.ActiveInputSource,source)&&ReferenceEquals(main.WorkflowTabViewModel.SelectedTab,workflow),
                (image,index,version)=>RunWorkflowWithImage(workflow,source,version,image,index),Dispatcher,()=>Window.GetWindow(this));
            ImagePreviewContent.Session=_sourceSession;
        }

        /// <summary>
        /// 当前选中图像用于本轮；成功完成后才推进到下一张。
        /// </summary>

        public static readonly DependencyProperty EditingAllowedProperty = DependencyProperty.Register(
            nameof(EditingAllowed),typeof(bool),typeof(ImageResultControl),new PropertyMetadata(true));
        public bool EditingAllowed => (bool)GetValue(EditingAllowedProperty);
        private void OnHostEditingStateChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs args)
        {
            if(_viewModel is null)return;
            var allowed=_viewModel.CanEditRootWorkspace&&_viewModel.WorkflowTabViewModel.SelectedTab?.IsInSubroutine!=true;
            SetCurrentValue(EditingAllowedProperty,allowed);
            ImagePreviewContent.SetCurrentValue(IsEnabledProperty,allowed);
            ModuleResultTreeView.IsReadOnly=!allowed;
            RefreshSourceSession();
        }

        public ImageResultControl()
        {
            InitializeComponent();
            _sourceTheme=new(ImagePreviewContent);
            _resultTheme=new(ModuleResultTreeView,SunEyeVision.UI.Services.Monitoring.ResultViewThemeFamily.PropertyList);
            ModuleResultTreeView.BindVariableCommand=new RelayCommand(
                value=>{if(value is FlatResultRow row)BindResultToGlobalVariable(row);},
                value=>EditingAllowed&&value is FlatResultRow {BindingInfo:not null} row&&_viewModel?.IsCurrentResultRow(row)==true);
            ModuleResultTreeView.CopyPathCommand=new RelayCommand(value=>
            {
                if(ModuleResultTreeView.SelectedItem is FlatResultRow row&&_viewModel?.IsCurrentResultRow(row)==true
                    &&value is string path&&!string.IsNullOrWhiteSpace(path))Clipboard.SetText(path);
            },value=>value is string {Length:>0}&&ModuleResultTreeView.SelectedItem is FlatResultRow row
                &&_viewModel?.IsCurrentResultRow(row)==true);
            Loaded+=(_,_)=>RefreshSourceSession();
            Unloaded+=(_,_)=>ReleaseSourceSession();
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
            if(_viewModel is not null)_viewModel.PropertyChanged-=OnHostEditingStateChanged;
            _viewModel = viewModel;
            // The visual DataContext is ImageResultViewModel; borrow the root presenter collection explicitly.
            ModuleResultTreeView.ItemsSource=viewModel.ResultTreeRoots;
            _viewModel.PropertyChanged+=OnHostEditingStateChanged;
            OnHostEditingStateChanged(this,new System.ComponentModel.PropertyChangedEventArgs(null));
            RefreshSourceSession();
        }

        /// <summary>
        /// 退出时退订图像预览事件
        /// </summary>
        public void Shutdown()
        {
            if(_viewModel is not null)_viewModel.PropertyChanged-=OnHostEditingStateChanged;
            ModuleResultTreeView.ItemsSource=null;
            _viewModel?.CloseResultPresentation();
            ReleaseSourceSession();
            _sourceTheme.Dispose();
            _resultTheme.Dispose();
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

        /// <summary>
        /// 从真实模块结果直接绑定全局变量。该入口与“全局变量管理器 → 输入来源”写入同一 ProducerBinding，
        /// 因而不会产生两套依赖配置。
        /// </summary>
        private void BindResultToGlobalVariable(FlatResultRow row)
        {
            if (!EditingAllowed||_viewModel?.IsCurrentResultRow(row)!=true||row.BindingInfo == null)
            {
                return;
            }

            var solutionManager=ServiceInitializer.SolutionManager;
            var expected=solutionManager.Session;
            var tab=_viewModel.WorkflowTabViewModel.SelectedTab;
            if(expected is null||tab is null)return;
            // Freeze schema identity before the modal picker. A switched document cannot reuse this row.
            var binding=new ProducerBinding
            {
                WorkflowId=tab.RootWorkflowId,NodeId=row.BindingInfo.SourceNodeId,
                ResultName=row.BindingInfo.ResultPath,SourceFieldId=row.BindingInfo.SourceFieldId,
                ResultDisplayName=row.DisplayName
            };
            var variable=GlobalVariableSelectDialog.ShowSelect(solutionManager,row.BindingInfo.ValueVocabulary);
            if(variable is null)return;
            if(!EditingAllowed||!_viewModel.IsCurrentResultRow(row))return;
            if(!_viewModel.TryBindResultVariable(expected,binding,variable.Id,out var error))
                ThemeMessageDialog.Show(error,"结果绑定未接纳",MessageBoxButton.OK,ThemeMessageDialog.MessageIcon.Warning);

        }

        /// <summary>先正式接纳明确选择，再由本轮配置快照执行。</summary>
        private async System.Threading.Tasks.Task RunWorkflowWithImage(WorkflowTabViewModel workflow,ImageInputSource source,long editVersion,ImageInfo image,int index)
        {
            var main=_viewModel;
            if(main is null||main.IsRunning||!ReferenceEquals(main.ActiveInputSource,source)||!ReferenceEquals(_sourceSession?.Source,source))return;
            try{if(!main.TrySelectPreviewImage(workflow,source,image,index,editVersion))return;}
            catch(Exception error)
            {
                _viewModel.AddLog(LogLevel.Error,"图像选择未接纳："+error.Message,LogSource.UIOperation);
                return;
            }
            if(!ReferenceEquals(_viewModel,main)||!ReferenceEquals(_sourceSession?.Source,source))return;
            var acceptedVersion=source.EditVersion;
            var workflowName = workflow.Name ?? "未知工作流";
            _viewModel.AddLog(LogLevel.Info, $"执行工作流 - 图像: {image.Name}", LogSource.Runtime(workflowName));
            _viewModel.AddLog(LogLevel.Info, $"路径: {image.FilePath}", LogSource.Runtime(workflowName));
            // 触发工作流执行（DisplayImage 由工作流结果驱动更新）
            _viewModel.StatusText = $"运行工作流: {workflow.Name} - {image.Name}";
            await main.RunPreviewImageAsync(workflow,source,image,index,acceptedVersion);
        }


        #region SDK ImageControl 事件处理

        /// <summary>
        /// DataGrid 结果行展开/折叠点击事件
        /// </summary>
        private void OnResultTreeSelectionChanged(object sender,RoutedPropertyChangedEventArgs<object> e)
        {
            ModuleResultTreeView.SelectedItemPath=e.NewValue is FlatResultRow row&&_viewModel?.IsCurrentResultRow(row)==true
                ?row.BindingInfo?.ResultPath??row.Key:string.Empty;
            CommandManager.InvalidateRequerySuggested();
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
