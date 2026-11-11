using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Shared;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Foundation.Logging;
using SunEyeVision.UI.Core;

using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Features.GlobalVariableManager;
using SunEyeVision.Plugin.Editing.Wpf.Models;
using SunEyeVision.Domain.Solutions;

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
        internal readonly SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources.ImageSourceStrip ImagePreviewContent;
        private MainWindowViewModel _viewModel = null!;
        private bool _shutdown;
        internal Func<string?> ReadClipboardText { get; set; } = () => Clipboard.ContainsText() ? Clipboard.GetText() : null;
        internal Action<string> WriteClipboardText { get; set; } = Clipboard.SetText;
        private SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.NativeMessageBox? _copyNotice;
        private System.Windows.Threading.DispatcherTimer? _copyNoticeTimer;
        private SunEyeVision.UI.Services.Images.ImageSourceProjectionBinding? _sourceBinding;

        private readonly SunEyeVision.UI.Services.Images.ImageSourceThemeScope _sourceTheme;
        private readonly SunEyeVision.UI.Services.Monitoring.ResultViewThemeScope _resultTheme;
        private readonly SunEyeVision.UI.Services.Monitoring.ResultViewThemeScope _shellTheme;
        private SunEyeVision.UI.Services.Images.ResultWorkspacePresenter? _workspacePresenter;
        private System.Collections.Specialized.INotifyCollectionChanged? _resultRoots;
        private void ReleaseResultSelection()
        {
            if(_resultRoots is not null)_resultRoots.CollectionChanged-=OnResultRootsChanged;
            _resultRoots=null;
            ModuleResultTreeView.SetCurrentValue(SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.PropertyListView.SelectedItemProperty,null);
            ModuleResultTreeView.SelectedItemPath=null;
            CommandManager.InvalidateRequerySuggested();
        }
        private void OnResultRootsChanged(object? sender,System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
        {
            if(ModuleResultTreeView.SelectedItem is FlatResultRow row&&_viewModel?.IsCurrentResultRow(row)==true)return;
            ModuleResultTreeView.SetCurrentValue(SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.PropertyListView.SelectedItemProperty,null);
            ModuleResultTreeView.SelectedItemPath=null;
            CommandManager.InvalidateRequerySuggested();
        }
        private void ReleaseSourceSession()
        {
            var binding=_sourceBinding;_sourceBinding=null;
            ImagePreviewContent.Session=null;ImagePreviewContent.IsRunning=false;
            binding?.SetDialogOwner(null);
        }
        private void RefreshSourceSession()
        {
            if(_shutdown||!IsLoaded||_viewModel is null){ReleaseSourceSession();return;}
            var binding=_viewModel.ActiveImageSourceBinding;
            if(ReferenceEquals(_sourceBinding,binding))return;
            ReleaseSourceSession();_sourceBinding=binding;
            if(binding is null)return;
            binding.SetDialogOwner(()=>Window.GetWindow(this));
            ImagePreviewContent.Session=binding.Strip;
        }

        public static readonly DependencyProperty EditingAllowedProperty = DependencyProperty.Register(
            nameof(EditingAllowed),typeof(bool),typeof(ImageResultControl),new PropertyMetadata(true));
        public bool EditingAllowed => (bool)GetValue(EditingAllowedProperty);
        private void OnHostEditingStateChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs args)
        {
            if(_viewModel is null)return;
            var allowed=_viewModel.CanEditResultBindings&&_viewModel.WorkflowTabViewModel.SelectedTab?.IsInSubroutine!=true;
            SetCurrentValue(EditingAllowedProperty,allowed);
            ImagePreviewContent.IsRunning=_viewModel.WorkflowTabViewModel.SelectedTab is {} tab&&_viewModel.IsFormalRootRunning(tab.RootWorkflowId);
            CommandManager.InvalidateRequerySuggested();
            // Keep original list focus through execution; formal source edits are guarded by the execution lease.
            ModuleResultTreeView.IsReadOnly=!allowed;
            RefreshSourceSession();
        }

        public ImageResultControl()
        {
            InitializeComponent();
            ModuleResultTreeView=(SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList.PropertyListView)Workspace.PropertyListContent;
            var sourceStrip=(Panel)Workspace.SourceStripContent;
            ImagePreviewContent=sourceStrip.Children.OfType<SunEyeVision.Plugin.Wpf.Controls.Native.Composition.ImageSources.ImageSourceStrip>().Single();
            RegisterName(nameof(ModuleResultTreeView),ModuleResultTreeView);
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
                    &&value is string path&&!string.IsNullOrWhiteSpace(path))CopyResultPath(row,path);
            },value=>value is string {Length:>0}&&ModuleResultTreeView.SelectedItem is FlatResultRow row
                &&_viewModel?.IsCurrentResultRow(row)==true);
            Loaded+=(_,_)=>{RefreshSourceSession();AttachWorkspace();};
            Unloaded+=(_,_)=>Cleanup(new Action[]{CloseCopyNotice,ReleaseSourceSession,ReleaseWorkspace});
        }

        private void CopyResultPath(FlatResultRow row,string path)
        {
            if(ReadClipboardText()==path)return;
            if(_shutdown||_viewModel?.IsCurrentResultRow(row)!=true||!ReferenceEquals(ModuleResultTreeView.SelectedItem,row))return;
            WriteClipboardText(path);
            CloseCopyNotice();
            var notice=SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.NativeMessageBox.CreateMessageBox(
                SunEyeVision.Plugin.Wpf.Native.Common.Localization.AppLocalizationService.GetLang("Lang_CopySuccess"),
                SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.MessageIcons.Ok,
                SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.MessageButtons.NoButton);
            if(notice is null)return;
            var owner=Window.GetWindow(this);
            if(owner is {IsVisible:true})notice.Owner=owner;
            _copyNotice=notice;
            var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1500)};
            _copyNoticeTimer=timer;timer.Tick+=CopyNoticeElapsed;
            notice.Show();timer.Start();
        }
        private void CopyNoticeElapsed(object? sender,EventArgs args)=>CloseCopyNotice();
        private void CloseCopyNotice()
        {
            var timer=_copyNoticeTimer;_copyNoticeTimer=null;
            if(timer is not null){timer.Stop();timer.Tick-=CopyNoticeElapsed;}
            var notice=_copyNotice;_copyNotice=null;notice?.Close();
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
            Workspace.CurrentResultSelectionChanged-=OnCurrentResultSelectionChanged;
            ReleaseResultSelection();
            ReleaseWorkspace();
            _viewModel = viewModel;
            System.Windows.Data.BindingOperations.SetBinding(Workspace,
                SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell.ResultWorkspaceShell.CurrentResultContentProperty,
                new System.Windows.Data.Binding(nameof(MainWindowViewModel.SelectedNodeDataAnalysisControl)){Source=viewModel});
            Workspace.CurrentResultSelectionChanged+=OnCurrentResultSelectionChanged;
            OnCurrentResultSelectionChanged(Workspace,EventArgs.Empty);
            foreach (var (property, path) in ResultHistoryBindings)
                Workspace.SetBinding(property, new System.Windows.Data.Binding(path) { Source = viewModel });
            // The visual DataContext is ImageResultViewModel; borrow the root presenter collection explicitly.
            ModuleResultTreeView.ItemsSource=viewModel.ResultTreeRoots;
            _resultRoots=(System.Collections.Specialized.INotifyCollectionChanged)viewModel.ResultTreeRoots;
            _resultRoots.CollectionChanged+=OnResultRootsChanged;
            _viewModel.PropertyChanged+=OnHostEditingStateChanged;
            OnHostEditingStateChanged(this,new System.ComponentModel.PropertyChangedEventArgs(null));
            RefreshSourceSession();AttachWorkspace();
        }

        private void OnCurrentResultSelectionChanged(object? sender, EventArgs e) =>
            _viewModel?.SetCurrentResultTableSelected(Workspace.IsCurrentResultSelected);

        /// <summary>
        /// 退出时退订图像预览事件
        /// </summary>
        private static void Cleanup(IEnumerable<Action> actions)
        {
            var failures=new List<Exception>();
            foreach(var action in actions)try{action();}catch(Exception error){failures.Add(error);}
            if(failures.Count>0)throw new AggregateException("结果宿主关闭失败，所有清理均已尝试。",failures);
        }
        private static readonly (DependencyProperty Property, string Path)[] ResultHistoryBindings =
        [
            (SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell.ResultWorkspaceShell.HistoryItemsProperty, nameof(MainWindowViewModel.ResultHistory)),
            (SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell.ResultWorkspaceShell.ClearHistoryCommandProperty, nameof(MainWindowViewModel.ClearResultHistoryCommand)),
            (SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell.ResultWorkspaceShell.SaveHistoryCommandProperty, nameof(MainWindowViewModel.SaveResultHistoryCommand))
        ];
        public void Shutdown()
        {
            if(_shutdown)return;_shutdown=true;
            var main=_viewModel;_viewModel=null!;
            Cleanup(new Action[]{
                ()=>{if(main is not null)main.PropertyChanged-=OnHostEditingStateChanged;},
                ()=>Workspace.CurrentResultSelectionChanged-=OnCurrentResultSelectionChanged,
                ReleaseResultSelection,
                ()=>ModuleResultTreeView.ItemsSource=null,
                ()=>main?.CloseResultPresentation(),CloseCopyNotice,ReleaseSourceSession,ReleaseWorkspace,
                ()=>System.Windows.Data.BindingOperations.ClearBinding(Workspace,
                    SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell.ResultWorkspaceShell.CurrentResultContentProperty),
                ()=>Cleanup(ResultHistoryBindings.Select(binding => (Action)(() => System.Windows.Data.BindingOperations.ClearBinding(Workspace, binding.Property)))),
                _sourceTheme.Dispose,_resultTheme.Dispose,_shellTheme.Dispose});
        }

        private Func<string,Func<bool>,SunEyeVision.Plugin.Wpf.Adapters.Regions.NativeRoiFrameLease?>? _roiBorrow;
        private MainWindowViewModel? _roiBorrowOwner;
        private void AttachWorkspace()
        {
            if(_shutdown||!IsLoaded||_viewModel is null||_workspacePresenter is not null)return;
            _workspacePresenter=new(_viewModel,Workspace);
            var presenter=_workspacePresenter;
            _roiBorrow=(reference,current)=>!_shutdown&&ReferenceEquals(_workspacePresenter,presenter)?presenter.BorrowRoiFrame(reference,current):null;
            _roiBorrowOwner=_viewModel;_viewModel.RuntimeRoiFrameBorrow=_roiBorrow;
        }
        private void ReleaseWorkspace()
        {
            if(_roiBorrowOwner is {} owner&&ReferenceEquals(owner.RuntimeRoiFrameBorrow,_roiBorrow))owner.RuntimeRoiFrameBorrow=null;
            _roiBorrowOwner=null;_roiBorrow=null;
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
            var expected=solutionManager.CurrentSession;var publication=main.CurrentResultPublication;
            if(expected is null||publication is null||!publication.IsCurrent())return;
            var reference=endpoint.Reference;
            var root=expected.Document.Workflows.SingleOrDefault(flow=>!flow.IsSubroutine&&flow.Id==reference.ScopeId);
            var node=root?.Nodes.SingleOrDefault(item=>item.Id==reference.NodeId);
            if(root is null||node is null)return;
            var binding=new ProducerBinding{ReferenceExpression=endpoint.ReferenceExpression,
                WorkflowName=root.Name,NodeName=node.DispName,ResultDisplayName=endpoint.ResultPath};
            var choices=expected.Document.GlobalVariables.Where(variable=>
                (remove?variable.ProducerBinding?.ReferenceExpression==binding.ReferenceExpression:main.IsResultVariableCandidate(expected,binding,variable))
                ).ToList();
            if(choices.Count==0)
            {
                ThemeMessageDialog.Show(remove?"该结果没有可解除的变量绑定。":"没有可承载该正式声明及存储的全局变量。","结果绑定",MessageBoxButton.OK,ThemeMessageDialog.MessageIcon.Warning);
                return;
            }
            var variable=remove&&choices.Count==1?choices[0]:GlobalVariableSelectionService.Choose(expected,choices,()=>ReferenceEquals(solutionManager.CurrentSession,expected)&&ReferenceEquals(_viewModel,main)&&EditingAllowed);
            if(variable is null)return;
            // The modal window may run arbitrary editing/navigation before it returns.
            if(!ReferenceEquals(_viewModel,main)||!EditingAllowed||!ReferenceEquals(solutionManager.CurrentSession,expected)
                ||!ReferenceEquals(main.CurrentResultPublication,publication)||!publication.IsCurrent()
                ||!main.IsCurrentResultRow(row)||!ReferenceEquals(row.BindingInfo,endpoint))return;
            var accepted=remove?main.TryUnbindResultVariable(expected,binding,variable,out var error)
                :main.TryBindResultVariable(expected,binding,variable,out error);
            if(!accepted)ThemeMessageDialog.Show(error,"结果绑定未接纳",MessageBoxButton.OK,ThemeMessageDialog.MessageIcon.Warning);
        }

        /// <summary>先正式接纳明确选择，再由本轮配置快照执行。</summary>



        #region 结果树交互

        /// <summary>
        /// DataGrid 结果行展开/折叠点击事件
        /// </summary>
        private void OnResultTreeSelectionChanged(object sender,RoutedPropertyChangedEventArgs<object> e)
        {
            ModuleResultTreeView.SelectedItemPath=e.NewValue is FlatResultRow row&&_viewModel?.IsCurrentResultRow(row)==true
                ?row.BindingInfo?.ResultPath??row.Key:null;
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion
    }
}
