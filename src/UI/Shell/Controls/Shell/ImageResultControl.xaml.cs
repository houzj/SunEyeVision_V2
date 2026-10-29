using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Shared;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        internal readonly SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.PropertyListView ModuleResultTreeView;
        internal readonly TextBlock SourceStatusMessage;
        internal readonly SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources.ImageSourceStrip ImagePreviewContent;
        private MainWindowViewModel _viewModel = null!;
        private bool _shutdown;
        private SunEyeVision.UI.Services.Images.ImageSourceStripSession? _sourceSession;
        private WorkflowTabViewModel? _sourceWorkflow;
        private readonly SunEyeVision.UI.Services.Images.ImageSourceThemeScope _sourceTheme;
        private readonly SunEyeVision.UI.Services.Monitoring.ResultViewThemeScope _resultTheme;
        private readonly SunEyeVision.UI.Services.Monitoring.ResultViewThemeScope _shellTheme;
        private SunEyeVision.UI.Services.Images.ResultWorkspacePresenter? _workspacePresenter;
        private void ReleaseSourceSession()
        {
            var session=_sourceSession;_sourceSession=null;_sourceWorkflow=null;
            Cleanup(new Action[]{()=>ImagePreviewContent.Session=null,()=>session?.Dispose()});
        }
        private void RefreshSourceSession()
        {
            if(_shutdown||!IsLoaded||_viewModel is null){ReleaseSourceSession();return;}
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

        public static readonly DependencyProperty EditingAllowedProperty = DependencyProperty.Register(
            nameof(EditingAllowed),typeof(bool),typeof(ImageResultControl),new PropertyMetadata(true));
        public bool EditingAllowed => (bool)GetValue(EditingAllowedProperty);
        private void OnHostEditingStateChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs args)
        {
            if(_viewModel is null)return;
            var allowed=_viewModel.CanEditRootWorkspace&&_viewModel.WorkflowTabViewModel.SelectedTab?.IsInSubroutine!=true;
            SetCurrentValue(EditingAllowedProperty,allowed);
            CommandManager.InvalidateRequerySuggested();
            ImagePreviewContent.SetCurrentValue(IsEnabledProperty,allowed);
            ModuleResultTreeView.IsReadOnly=!allowed;
            RefreshSourceSession();
        }

        public ImageResultControl()
        {
            InitializeComponent();
            ModuleResultTreeView=(SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.PropertyListView)Workspace.PropertyListContent;
            var sourceStrip=(Panel)Workspace.SourceStripContent;
            SourceStatusMessage=sourceStrip.Children.OfType<TextBlock>().Single();
            ImagePreviewContent=sourceStrip.Children.OfType<SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources.ImageSourceStrip>().Single();
            RegisterName(nameof(ModuleResultTreeView),ModuleResultTreeView);
            RegisterName(nameof(SourceStatusMessage),SourceStatusMessage);
            RegisterName(nameof(ImagePreviewContent),ImagePreviewContent);
            _sourceTheme=new(ImagePreviewContent);
            _resultTheme=new(ModuleResultTreeView,SunEyeVision.UI.Services.Monitoring.ResultViewThemeFamily.PropertyList);
            _shellTheme=new(Workspace,SunEyeVision.UI.Services.Monitoring.ResultViewThemeFamily.ResultShell);
            Workspace.PreviousImageCommand=new RelayCommand(async ()=>
            { if(_viewModel is {} viewModel)await viewModel.NavigateLocalImagesAsync(-1); },
                ()=>_viewModel?.CanNavigateLocalImages()==true);
            Workspace.NextImageCommand=new RelayCommand(async ()=>
            { if(_viewModel is {} viewModel)await viewModel.NavigateLocalImagesAsync(1); },
                ()=>_viewModel?.CanNavigateLocalImages()==true);
            ModuleResultTreeView.BindVariableCommand=new RelayCommand(
                value=>{if(value is FlatResultRow row)BindResultToGlobalVariable(row);},
                value=>EditingAllowed&&value is FlatResultRow {IsBindable:true} row&&_viewModel?.IsCurrentResultRow(row)==true);
            ModuleResultTreeView.UnbindVariableCommand=new RelayCommand(
                value=>{if(value is FlatResultRow row)EditResultVariable(row,true);},
                value=>EditingAllowed&&value is FlatResultRow {IsBindable:true} row&&_viewModel?.IsCurrentResultRow(row)==true);
            ModuleResultTreeView.CopyPathCommand=new RelayCommand(value=>
            {
                if(ModuleResultTreeView.SelectedItem is FlatResultRow row&&_viewModel?.IsCurrentResultRow(row)==true
                    &&value is string path&&!string.IsNullOrWhiteSpace(path))Clipboard.SetText(path);
            },value=>value is string {Length:>0}&&ModuleResultTreeView.SelectedItem is FlatResultRow row
                &&_viewModel?.IsCurrentResultRow(row)==true);
            Loaded+=(_,_)=>{RefreshSourceSession();AttachWorkspace();};
            Unloaded+=(_,_)=>Cleanup(new Action[]{ReleaseSourceSession,ReleaseWorkspace});
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
            ObjectDisposedException.ThrowIf(_shutdown,this);
            if(_viewModel is not null)_viewModel.PropertyChanged-=OnHostEditingStateChanged;
            ReleaseWorkspace();
            _viewModel = viewModel;
            System.Windows.Data.BindingOperations.SetBinding(SourceStatusMessage,System.Windows.Controls.TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(MainWindowViewModel.ImagePreviewSourceMessage)){Source=viewModel});
            System.Windows.Data.BindingOperations.SetBinding(Workspace,
                SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell.ResultWorkspaceShell.CurrentResultContentProperty,
                new System.Windows.Data.Binding(nameof(MainWindowViewModel.SelectedNodeDataAnalysisControl)){Source=viewModel});
            // The visual DataContext is ImageResultViewModel; borrow the root presenter collection explicitly.
            ModuleResultTreeView.ItemsSource=viewModel.ResultTreeRoots;
            _viewModel.PropertyChanged+=OnHostEditingStateChanged;
            OnHostEditingStateChanged(this,new System.ComponentModel.PropertyChangedEventArgs(null));
            RefreshSourceSession();AttachWorkspace();
        }

        /// <summary>
        /// 退出时退订图像预览事件
        /// </summary>
        private static void Cleanup(IEnumerable<Action> actions)
        {
            var failures=new List<Exception>();
            foreach(var action in actions)try{action();}catch(Exception error){failures.Add(error);}
            if(failures.Count>0)throw new AggregateException("结果宿主关闭失败，所有清理均已尝试。",failures);
        }
        public void Shutdown()
        {
            if(_shutdown)return;_shutdown=true;
            var main=_viewModel;_viewModel=null!;
            Cleanup(new Action[]{
                ()=>{if(main is not null)main.PropertyChanged-=OnHostEditingStateChanged;},
                ()=>ModuleResultTreeView.ItemsSource=null,
                ()=>main?.CloseResultPresentation(),ReleaseSourceSession,ReleaseWorkspace,
                ()=>System.Windows.Data.BindingOperations.ClearBinding(Workspace,
                    SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell.ResultWorkspaceShell.CurrentResultContentProperty),
                ()=>System.Windows.Data.BindingOperations.ClearBinding(SourceStatusMessage,System.Windows.Controls.TextBlock.TextProperty),
                _sourceTheme.Dispose,_resultTheme.Dispose,_shellTheme.Dispose});
        }

        private void AttachWorkspace()
        {
            if(_shutdown||!IsLoaded||_viewModel is null||_workspacePresenter is not null)return;
            _workspacePresenter=new(_viewModel,Workspace);
        }
        private void ReleaseWorkspace()
        {
            var presenter=_workspacePresenter;_workspacePresenter=null;presenter?.Dispose();
        }
        public void ToggleFullscreen()
        {
            if(Workspace.FullscreenCommand?.CanExecute(Workspace.OutputViewport)==true)
                Workspace.FullscreenCommand.Execute(Workspace.OutputViewport);
        }

        /// <summary>
        /// 图像预览请求执行工作流（原 MainWindow.OnWorkflowExecutionRequested）
        /// </summary>

        /// <summary>
        /// 从真实模块结果直接绑定全局变量。该入口与“全局变量管理器 → 输入来源”写入同一 ProducerBinding，
        /// 因而不会产生两套依赖配置。
        /// </summary>
        private void BindResultToGlobalVariable(FlatResultRow row)=>EditResultVariable(row,false);
        private void EditResultVariable(FlatResultRow row,bool remove)
        {
            var main=_viewModel;
            if(!EditingAllowed||main?.IsCurrentResultRow(row)!=true||row.BindingInfo is not {} endpoint||!row.IsBindable)return;
            var solutionManager=ServiceInitializer.SolutionManager;
            var expected=solutionManager.Session;var publication=main.CurrentResultPublication;
            if(expected is null||publication is null||!publication.IsCurrent())return;
            var reference=endpoint.Reference;
            var root=expected.Document.Workflows.SingleOrDefault(flow=>!flow.IsSubroutine&&flow.Id==reference.ScopeId);
            var node=root?.Nodes.SingleOrDefault(item=>item.Id==reference.NodeId);
            if(root is null||node is null)return;
            var binding=new ProducerBinding{ReferenceExpression=endpoint.ReferenceExpression,
                WorkflowName=root.Name,NodeName=node.DispName,ResultDisplayName=endpoint.ResultPath};
            var choices=expected.Document.GlobalVariables.Where(variable=>
                (!remove||variable.ProducerBinding?.ReferenceExpression==binding.ReferenceExpression)
                &&GlobalProducerBindingRules.Validate(expected.Document,binding,variable).State is
                    SunEyeVision.Plugin.Contracts.BindingCompatibility.Compatible or SunEyeVision.Plugin.Contracts.BindingCompatibility.Conditional).ToList();
            if(choices.Count==0)
            {
                ThemeMessageDialog.Show(remove?"该结果没有可解除的变量绑定。":"没有可承载该正式声明及存储的全局变量。","结果绑定",MessageBoxButton.OK,ThemeMessageDialog.MessageIcon.Warning);
                return;
            }
            var variable=GlobalVariableSelectDialog.ShowSelect(choices);
            if(variable is null)return;
            // The modal window may run arbitrary editing/navigation before it returns.
            if(!ReferenceEquals(_viewModel,main)||!EditingAllowed||!ReferenceEquals(solutionManager.Session,expected)
                ||!ReferenceEquals(main.CurrentResultPublication,publication)||!publication.IsCurrent()
                ||!main.IsCurrentResultRow(row)||!ReferenceEquals(row.BindingInfo,endpoint))return;
            var accepted=remove?main.TryUnbindResultVariable(expected,binding,variable.Id,out var error)
                :main.TryBindResultVariable(expected,binding,variable.Id,out error);
            if(!accepted)ThemeMessageDialog.Show(error,"结果绑定未接纳",MessageBoxButton.OK,ThemeMessageDialog.MessageIcon.Warning);
        }

        /// <summary>先正式接纳明确选择，再由本轮配置快照执行。</summary>
        private async System.Threading.Tasks.Task RunWorkflowWithImage(WorkflowTabViewModel workflow,ImageInputSource source,long editVersion,ImageInfo image,int index)
        {
            var main=_viewModel;
            if(_shutdown||main is null||main.IsRunning||!ReferenceEquals(main.ActiveInputSource,source)||!ReferenceEquals(_sourceSession?.Source,source))return;
            try{if(!main.TrySelectPreviewImage(workflow,source,image,index,editVersion))return;}
            catch(Exception error)
            {
                if(!_shutdown&&ReferenceEquals(_viewModel,main))main.AddLog(LogLevel.Error,"图像选择未接纳："+error.Message,LogSource.UIOperation);
                return;
            }
            if(_shutdown||!ReferenceEquals(_viewModel,main)||!ReferenceEquals(_sourceSession?.Source,source))return;
            var acceptedVersion=source.EditVersion;
            var workflowName = workflow.Name ?? "未知工作流";
            main.AddLog(LogLevel.Info, $"执行工作流 - 图像: {image.Name}", LogSource.Runtime(workflowName));
            main.AddLog(LogLevel.Info, $"路径: {image.FilePath}", LogSource.Runtime(workflowName));
            // 触发工作流执行（DisplayImage 由工作流结果驱动更新）
            if(_shutdown||!ReferenceEquals(_viewModel,main)||!ReferenceEquals(_sourceSession?.Source,source))return;
            main.StatusText = $"运行工作流: {workflow.Name} - {image.Name}";
            if(_shutdown||!ReferenceEquals(_viewModel,main)||!ReferenceEquals(_sourceSession?.Source,source))return;
            await main.RunPreviewImageAsync(workflow,source,image,index,acceptedVersion);
        }


        #region 结果树交互

        /// <summary>
        /// DataGrid 结果行展开/折叠点击事件
        /// </summary>
        private void OnResultTreeSelectionChanged(object sender,RoutedPropertyChangedEventArgs<object> e)
        {
            ModuleResultTreeView.SelectedItemPath=e.NewValue is FlatResultRow row&&_viewModel?.IsCurrentResultRow(row)==true
                ?row.BindingInfo?.ResultPath??row.Key:string.Empty;
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion
    }
}
