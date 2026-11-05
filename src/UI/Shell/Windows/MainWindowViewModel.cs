using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Plugin.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.UI.Services.Monitoring;
using SunEyeVision.Plugin.Wpf.Presenters.Results;


using SunEyeVision.Plugin.SDK;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.UI;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Shell;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Converters.Path;
using SunEyeVision.UI.Services.Logging;
using SunEyeVision.Plugin.SDK.Logging;

using SunEyeVision.UI.Shell.Controls.Toolbox;

using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Shell.Windows
{
    /// <summary>
    /// 结果项
    /// </summary>
    public class ResultItem
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }


    /// <summary>
    /// 主窗口视图模型
    /// </summary>
    public partial class MainWindowViewModel : ViewModelBase, Services.Workflow.INodeResultSink, SunEyeVision.UI.Core.IRootCoordinator
    {
        // Set by the actual MainWindow host; never infer ownership from the active/global window.
        internal Window? SolutionDialogOwner { get; set; }
        private Window? ResolveSolutionDialogOwner(Window? owner)
        {
            owner ??= SolutionDialogOwner;
            if (owner is null) return null;
            owner.Dispatcher.VerifyAccess();
            // Startup may prepare a document before its host is shown; a closed host is not a valid Owner.
            return owner.IsLoaded && owner.IsVisible && !owner.Dispatcher.HasShutdownStarted ? owner : null;
        }
        private string _title = "太阳眼视觉";
        private string _status = "";
        private string _currentCanvasTypeText = "原 Diagram (测试)";

        // 图像显示
        private double _imageScale = 1.0;

        // 图像预览
        private bool _autoSwitchEnabled = true;

        // 参数选择状态（用于 TreeView 事件处理）
        private string? _selectedParameterName;

        // 所有工作流状态
        private bool _isAllWorkflowsRunning = false;
        private string _allWorkflowsRunButtonText = "运行";

        // 工作流文件管理


        // 工作流引擎

        /// <summary>公开工作流引擎访问（供 MainWindow code-behind 等使用）</summary>
        /// <summary>工作流定义的统一注册、创建和方案挂接入口。</summary>
        private readonly ILogger _logger;

        // 执行管理


        // 图像结果子 VM（就近绑定右侧图像结果区）
        private readonly SunEyeVision.UI.Shell.Controls.ImageResultViewModel _imageResult;

        // 状态栏子 VM（就近绑定底部状态栏）
        private readonly SunEyeVision.UI.Shell.Controls.StatusBarViewModel _statusBar;
        private readonly Services.Workflow.WorkflowExecutionSnapshotPresenter _executionSnapshotPresenter = new();

        // 顶部标题栏子 VM（就近绑定顶部菜单+工具栏）
        private readonly SunEyeVision.UI.Shell.Controls.ShellTopBarViewModel _topBar;

        // 工作区子 VM（就近绑定中间工作区）
        private readonly SunEyeVision.UI.Shell.Controls.WorkspaceViewModel _workspace;

        // 图像显示数据源集合（与图像结果子 VM 共享同一实例）


        // 扁平化结果行集合（与图像结果子 VM 共享同一实例）
        private readonly ObservableCollection<FlatResultRow> _flatResultRows = new();

        // 运行时参数服务（编辑态 UI 层运行时参数）

        // 工作流上下文服务（按工作流ID缓存独立执行上下文）


        // 节点结果管理


        // 已打开的调试窗口（全局单例）
        private Window? _openDebugWindow;

        /// <summary>
        /// 调试窗口是否打开（用于锁定画布操作，避免Delete键冲突）
        /// </summary>
        public bool IsDebugWindowOpen => (_openDebugWindow != null && _openDebugWindow.IsLoaded) || _auxiliaryParameterWindows.Count != 0;

        // 按 Workflow 缓存的 DSQS（key: WorkflowId，替代旧的按节点缓存的 _nodeDataProviders）


        // 调试窗口缓存（用于在节点执行完成后通知 ToolDebugControl 刷新运行时状态）


        private string _logText = "[系统] 等待中...\n";
        private bool _hasModuleResults;

        // 折叠状态
        private bool _isToolboxCollapsed = true;
        private bool _isImageDisplayCollapsed = false;
        private bool _isRightPanelCollapsed = false;
        private double _toolboxWidth = 260;
        private double _rightPanelWidth = 500;
        private double _imageDisplayHeight = 500;

        // 分割器
        private double _splitterPosition = 500; // 默认图像高度
        private const double MinImageAreaHeight = 200;
        private const double MaxImageAreaHeight = 800;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public bool IsRunning => _formalActiveBatch is not null || _formalContinuousRun is not null;
        private void NotifyFormalRunState()
        {
            Status=FormalExecutionStateText;
            ActiveImageSourceBinding?.RefreshAvailability();
            foreach(var editor in _variableEditors.ToArray())editor.RefreshAvailability();
            OnPropertyChanged(nameof(IsRunning));OnPropertyChanged(nameof(IsContinuousRunning));OnPropertyChanged(nameof(IsExecutionPaused));OnPropertyChanged(nameof(IsExecutionStopping));OnPropertyChanged(nameof(CanEditWorkflowCanvas));OnPropertyChanged(nameof(CanEditRootWorkspace));OnPropertyChanged(nameof(CanEditRootCollection));
            OnWorkflowStatusChanged(WorkflowTabViewModel,EventArgs.Empty);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// 工作流数量（跨域共享，状态栏显示）
        /// </summary>
        public int WorkflowCount => WorkflowTabViewModel?.Tabs?.Count ?? 0;

        /// <summary>
        /// 当前画布类型显示文本
        /// </summary>
        public string CurrentCanvasTypeText
        {
            get => _currentCanvasTypeText;
            set => SetProperty(ref _currentCanvasTypeText, value);
        }

        public ToolboxViewModel Toolbox { get; }

        // 注意：已删除全局WorkflowNodes和WorkflowConnections集合
        // 所有节点和连接现在通过 WorkflowTabViewModel.SelectedTab 访问
        // 确保每个Tab都是独立的

        private NodeDefinition? _selectedNode;
        private long _outputContractSelectionGeneration;

        /// <summary>
        /// 当前活动输入源，用于绑定到图像预览控件
        /// 输入源存储用户选择的图像文件，执行流程不会修改
        /// </summary>
        public SunEyeVision.UI.Shared.ImageInputSource? ActiveInputSource => ActiveImageSourceBinding?.Source;



        /// <summary>
        /// 图像显示控件的数据源（当前节点 + 所有父节点）
        /// </summary>
        /// <summary>
        /// 图像结果子 VM（就近绑定右侧图像结果区）
        /// </summary>
        public SunEyeVision.UI.Shell.Controls.ImageResultViewModel ImageResult => _imageResult;

        /// <summary>
        /// 状态栏子 VM（就近绑定底部状态栏）
        /// </summary>
        public SunEyeVision.UI.Shell.Controls.StatusBarViewModel StatusBar => _statusBar;

        /// <summary>
        /// 顶部标题栏子 VM（就近绑定顶部菜单+工具栏）
        /// </summary>
        public SunEyeVision.UI.Shell.Controls.ShellTopBarViewModel TopBar => _topBar;

        /// <summary>
        /// 工作区子 VM（就近绑定中间工作区）
        /// </summary>
        public SunEyeVision.UI.Shell.Controls.WorkspaceViewModel Workspace => _workspace;

        // ===== IRootCoordinator 显式实现（类型不匹配的成员）=====

        object? SunEyeVision.UI.Core.IRootCoordinator.SelectedTab => WorkflowTabViewModel.SelectedTab;

        /// <summary>
        /// 设置当前选中工作流标签页（唯一写入口，实现 IRootCoordinator）。
        /// 用户操作与代码路径统一经此方法变更，避免 SelectedTab 双向绑定回写造成重入。
        /// </summary>
        /// <param name="tab">目标标签页（WorkflowTabViewModel 实例）</param>
        public void SetSelectedTab(object? tab)
            => WorkflowTabViewModel.SelectedTab = tab as WorkflowTabViewModel;

        System.Collections.IList SunEyeVision.UI.Core.IRootCoordinator.Tabs => WorkflowTabViewModel.Tabs;

        // ========== 数据分析控件相关 ==========
        private bool _hasDataAnalysisControl = false;
        private UIElement? _selectedNodeDataAnalysisControl = null;
        private string _dataAnalysisControlTitle = "数据分析";

        /// <summary>
        /// 当前选中节点是否有数据分析控件
        /// </summary>
        public bool HasDataAnalysisControl
        {
            get => _hasDataAnalysisControl;
            set => SetProperty(ref _hasDataAnalysisControl, value);
        }

        /// <summary>
        /// 当前选中节点的数据分析控件
        /// </summary>
        public UIElement? SelectedNodeDataAnalysisControl
        {
            get => _selectedNodeDataAnalysisControl;
            set => SetProperty(ref _selectedNodeDataAnalysisControl, value);
        }

        /// <summary>
        /// 数据分析控件标题
        /// </summary>
        public string DataAnalysisControlTitle
        {
            get => _dataAnalysisControlTitle;
            set => SetProperty(ref _dataAnalysisControlTitle, value);
        }

        public NodeDefinition? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (value is not null && !OwnsSelectedDocumentNode(value)) throw new InvalidOperationException("节点不属于当前实际方案与流程。");
                if (SetSelectedNodeIdentity(value)) RefreshFormalNodeSelection();
            }
        }

        /// <summary>
        /// 强制选中节点（即使引用相同也强制更新显示）
        /// 用于双击节点时确保图像显示正确更新
        /// </summary>
        /// <remarks>
        /// ★ 防闪烁优化：移除不必要的清空操作，直接刷新显示
        /// </remarks>
        /// <param name="node">要选中的节点</param>
        public void ForceSelectNode(NodeDefinition node)
        {
            if (!OwnsSelectedDocumentNode(node)) throw new InvalidOperationException("节点实例已退出当前方案。");
            if (ReferenceEquals(SelectedNode,node)) RefreshFormalNodeSelection(); else SelectedNode=node;
        }

        public CanvasConnectionProjection? SelectedConnection { get; set; }

        // 工作流标签页
        public WorkflowTabControlViewModel WorkflowTabViewModel { get; }

        /// <summary>
        /// 当前选中工作流标签页变化事件（代码路径切换 Tab 时通知子 VM 刷新绑定，如 WorkspaceViewModel）
        /// </summary>
        public event EventHandler? SelectedTabChanged;

        /// <summary>
        /// 当前选中的工作流标签页的命令管理器
        /// 每个标签页拥有独立的命令管理器
        /// </summary>

        public string StatusText
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        // 当前解决方案信息
        private static SunEyeVision.Application.DocumentSessionManager DocumentManager => SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager;
        public string CurrentSolutionName => DocumentManager.CurrentSession?.Document is { } document
            ? (string.IsNullOrWhiteSpace(document.Name) ? "未命名方案" : document.Name) : "";
        public string CurrentSolutionPath => DocumentManager.CurrentSession?.LastWrittenSnapshot?.TargetPath ?? "";
        public bool HasCurrentSolution => DocumentManager.CurrentSession != null;
        public bool HasUnsavedSolutionChanges => DocumentManager.CurrentSession?.HasUnsavedChanges==true;

        public string CameraStatus => "相机(2台)";

        // 图像显示相关


        /// <summary>
        /// 计算结果
        /// </summary>
        public ObservableCollection<ResultItem> CalculationResults { get; }

        /// <summary>
        /// 模块结果树（分组可折叠显示）
        /// </summary>
        public ObservableCollection<ModuleResultNode> ModuleResultTree { get; } = new();

        /// <summary>
        /// 扁平化结果行列表（用于 DataGrid 绑定，与图像结果子 VM 共享同一集合实例）
        /// </summary>
        public ObservableCollection<FlatResultRow> FlatResultRows => _flatResultRows;
        public ReadOnlyObservableCollection<FlatResultRow> ResultTreeRoots=>DeclaredResultPresenter.Roots;
        internal bool IsCurrentResultRow(FlatResultRow row)=>_declaredResultActive
            &&_declaredResultPresenter?.ContainsRow(row)==true;
        private SunEyeVision.UI.Services.Monitoring.ResultPresentationPresenter? _declaredResultPresenter;
        private bool _declaredResultActive;
        private bool _declaredHistoricalResult;
        private SunEyeVision.UI.Services.Monitoring.ResultPresentationPresenter DeclaredResultPresenter
            =>_declaredResultPresenter??=new(FlatResultRows,value=>HasModuleResults=value,
                projectionFailed:publication=>{if(ReferenceEquals(_resultPublication,publication))RetireResultPublication();});

        /// <summary>
        /// 是否有模块结果（用于空状态显示）
        /// </summary>
        public bool HasModuleResults
        {
            get => _imageResult.HasModuleResults;
            set => _imageResult.HasModuleResults = value;
        }

        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        // 折叠状态
        public bool IsToolboxCollapsed
        {
            get => _isToolboxCollapsed;
            set => SetProperty(ref _isToolboxCollapsed, value);
        }

        public bool IsImageDisplayCollapsed
        {
            get => _isImageDisplayCollapsed;
            set => SetProperty(ref _isImageDisplayCollapsed, value);
        }

        public bool IsRightPanelCollapsed
        {
            get => _isRightPanelCollapsed;
            set => SetProperty(ref _isRightPanelCollapsed, value);
        }

        public double ToolboxWidth
        {
            get => _toolboxWidth;
            set => SetProperty(ref _toolboxWidth, value);
        }

        public double RightPanelWidth
        {
            get => _rightPanelWidth;
            set => SetProperty(ref _rightPanelWidth, value);
        }

        public double ImageDisplayHeight
        {
            get => _imageDisplayHeight;
            set => SetProperty(ref _imageDisplayHeight, value);
        }

        /// <summary>
        /// 图像显示区域高度，分割器上方
        /// </summary>
        public double SplitterPosition
        {
            get => _splitterPosition;
            private set
            {
                // 确保在合理范围
                value = Math.Max(MinImageAreaHeight, Math.Min(MaxImageAreaHeight, value));
                if (Math.Abs(_splitterPosition - value) > 1) // 忽略微小变化
                {
                    _splitterPosition = value;
                    OnPropertyChanged(nameof(SplitterPosition));
                }
            }
        }

        /// <summary>
        /// 保存分割器位置（从控件调用）
        /// </summary>
        public void SaveSplitterPosition(double position)
        {
SplitterPosition = position;

            // TODO: 选保存到用户设置
            // Settings.Default.SplitterPosition = position;
            // Settings.Default.Save();
        }

        /// <summary>
        /// 所有工作流是否正在运行
        /// </summary>
        public bool IsAllWorkflowsRunning
        {
            get => _isAllWorkflowsRunning;
            set => SetProperty(ref _isAllWorkflowsRunning, value);
        }

        /// <summary>
        /// 所有工作流按钮文本
        /// </summary>
        public string AllWorkflowsRunButtonText
        {
            get => _allWorkflowsRunButtonText;
            set => SetProperty(ref _allWorkflowsRunButtonText, value);
        }


        /// <summary>
        /// 是否自动切换
        /// </summary>
        public bool AutoSwitchEnabled
        {
            get => _autoSwitchEnabled;
            set => SetProperty(ref _autoSwitchEnabled, value);
        }

        /// <summary>
        /// 添加日志（智能迁移版本 - 自动解析Emoji和来源）
        /// </summary>
        public void AddLog(string message)
        {
            var (level, cleanMessage, source) = ParseLegacyLogMessage(message);
            VisionLogger.Instance.Log(level, cleanMessage, source);
        }

        /// <summary>
        /// 添加带级别的日志
        /// </summary>
        public void AddLog(LogLevel level, string message, string? source = null)
        {
            VisionLogger.Instance.Log(level, message, source);
        }

        /// <summary>
        /// 解析遗留日志消息，提取级别、来源和纯净消息
        /// </summary>
        private static (LogLevel level, string message, string source) ParseLegacyLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return (LogLevel.Info, message ?? string.Empty, LogSource.UI("默认"));

            var level = LogLevel.Info;
            var source = LogSource.UI("默认");
            var cleanMessage = message;

            // 解析Emoji前缀确定日志级别
            if (message.StartsWith("✅") || message.StartsWith("✓") || message.StartsWith("✔"))
            {
                level = LogLevel.Success;
                cleanMessage = message.Substring(1).TrimStart();
            }
            else if (message.StartsWith("❌") || message.StartsWith("✖") || message.StartsWith("✗"))
            {
                level = LogLevel.Error;
                cleanMessage = message.Substring(1).TrimStart();
            }
            else if (message.StartsWith("⚠️") || message.StartsWith("⚠"))
            {
                level = LogLevel.Warning;
                cleanMessage = message.Substring(message.StartsWith("⚠️") ? 2 : 1).TrimStart();
            }
            else if (message.StartsWith("🗑️") || message.StartsWith("🔧") || message.StartsWith("📝"))
            {
                level = LogLevel.Info;
                cleanMessage = message.Substring(message.StartsWith("🗑️") ? 2 : 1).TrimStart();
            }

            // 解析[来源]标记
            var sourceMatch = System.Text.RegularExpressions.Regex.Match(cleanMessage, @"^\[([^\]]+)\]\s*");
            if (sourceMatch.Success)
            {
                var sourceTag = sourceMatch.Groups[1].Value;
                cleanMessage = cleanMessage.Substring(sourceMatch.Length);

                // 映射来源标签到LogSource
                source = sourceTag switch
                {
                    "系统" => LogSource.SystemCore,
                    "设备" => LogSource.DeviceCamera,
                    "运行" => LogSource.UIOperation,
                    "Connection" => LogSource.UIConnection,
                    _ => LogSource.UI(sourceTag)
                };
            }

            // 根据消息内容推断来源
            if (cleanMessage.Contains("节点"))
                source = LogSource.UINode;
            else if (cleanMessage.Contains("连接"))
                source = LogSource.UIConnection;
            else if (cleanMessage.Contains("工作流"))
                source = LogSource.UIOperation;
            else if (cleanMessage.Contains("撤销") || cleanMessage.Contains("重做"))
                source = LogSource.UIEdit;
            else if (cleanMessage.Contains("调试窗口"))
                source = LogSource.UIDebug;
            else if (cleanMessage.Contains("运行时参数"))
                source = LogSource.UIConfig;
            else if (cleanMessage.Contains("缓存"))
                source = LogSource.UIOperation;

            return (level, cleanMessage, source);
        }

        /// <summary>
        /// 清空日志
        /// </summary>
        public void ClearLog()
        {
            // 使用日志管理器清空
            // 注意：VisionLogger 不支持直接清空，这里仅作标记
            // TODO: 实现日志清空功能
        }
        // 解决方案保存命令
        public ICommand SaveCurrentSolutionCommand { get; }
        public ICommand SaveCurrentSolutionAsCommand { get; }

        public ICommand RunWorkflowCommand { get; }
        public ICommand StopWorkflowCommand { get; }
        public ICommand ShowSettingsCommand { get; }
        public ICommand ShowAboutCommand { get; }
        public ICommand ShowHelpCommand { get; }
        public ICommand ShowShortcutsCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand DeleteSelectedNodesCommand { get; }
        public ICommand OpenDebugWindowCommand { get; }
        public ICommand TogglePathPointsCommand { get; }

        // 所有工作流
        public ICommand RunAllWorkflowsCommand { get; }
        public ICommand ToggleContinuousAllCommand { get; }

        // 图像视图

        // 图像操作
        public ICommand ClearImageCommand { get; }

        public ICommand ClearLogCommand { get; }

        // 解决方案管理

        // 管理器
        public ICommand ShowGlobalVariableManagerCommand { get; }
        public ICommand ShowCameraManagerCommand { get; }
        public ICommand ShowCommunicationManagerCommand { get; }

        private readonly SunEyeVision.Plugin.Runtime.RuntimeDirectorySnapshot _directory;
        public MainWindowViewModel(SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DesktopToolAdmission admission)
        {
            _directory=admission?.Runtime??throw new ArgumentNullException(nameof(admission));
            try
            {
            Toolbox = new ToolboxViewModel(admission);

            WorkflowTabViewModel = new WorkflowTabControlViewModel(_directory);

            // 初始化计算结果
            CalculationResults = new ObservableCollection<ResultItem>();

            // 初始化图像集合（优化版）

            // 初始化共享基础设施服务


            // 初始化图像结果子 VM（就近绑定右侧图像结果区，共享数据源/结果行集合）
            _imageResult = new SunEyeVision.UI.Shell.Controls.ImageResultViewModel(this);
            _imageResult.AttachCollections(_flatResultRows);

            // 初始化状态栏子 VM（就近绑定底部状态栏）
            _statusBar = new SunEyeVision.UI.Shell.Controls.StatusBarViewModel(this);
            _statusBar.ShowExecutionSnapshotRequested = ShowExecutionSnapshot;

            // 初始化顶部标题栏子 VM（就近绑定顶部菜单+工具栏）
            _topBar = new SunEyeVision.UI.Shell.Controls.ShellTopBarViewModel(this);

            // 初始化工作区子 VM（就近绑定中间工作区）
            _workspace = new SunEyeVision.UI.Shell.Controls.WorkspaceViewModel(this);

            // 初始化工作流引擎
            _logger = VisionLogger.Instance;

            // 初始化执行管理器（传入共享的 WorkflowEngine，子程序工作流已注册在其中）
            WorkflowTabViewModel.StartContinuousExecution = StartContinuousWorkflow;
            WorkflowTabViewModel.StopContinuousExecution = StopContinuousWorkflow;
            WorkflowTabViewModel.StartAllContinuousExecution = StartAllContinuousWorkflows;
            WorkflowTabViewModel.StopAllContinuousExecution = () => { if(!IsRunning)return false; RequestStopFormalExecution(); return true; };

            // 初始化节点结果管理器


            // 订阅执行管理器事件
            // 连续执行每轮开始：自动切换开启时推进输入图并刷新运行时参数

            // 初始化当前画布类型
            UpdateCurrentCanvasType();

            // 选中标签页变化时更新运行/停止按钮状态
            WorkflowTabViewModel.SelectionChanged += OnSelectedTabChanged;

            // 监听新 Tab 创建，自动订阅 根流程名称请求 事件
            WorkflowTabViewModel.Tabs.CollectionChanged += OnTabsCollectionChanged;

            // 为已存在的 Tab（如默认工作流）订阅 根流程名称请求
            foreach (var tab in WorkflowTabViewModel.Tabs)
            {
                tab.RenameRequested = name => RenameRootWorkspace(tab,name);
                EnsureFormalExecutionSnapshot(tab.RootWorkflowId);
            }

            // 订阅工作流状态变化
            WorkflowTabViewModel.WorkflowStatusChanged += OnWorkflowStatusChanged;

            // 订阅控制流节点打开事件（由 NodeOpenDecisionChain 统一路由）


            // 命令管理器的初始化


            // 监听解决方案变更事件
            var solutionManager = SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager;
            ConfigureFormalImageSourcePresentation();
            WorkflowTabViewModel.BindDocumentManager(solutionManager);

            // 初始化当前解决方案信息
            UpdateCurrentSolutionInfo();
            // 解决方案保存命令
            SaveCurrentSolutionCommand = new RelayCommand(ExecuteSaveCurrentSolution, () => DocumentManager.CurrentSession is not null && !IsRunning);
            SaveCurrentSolutionAsCommand = new RelayCommand(() => SaveCurrentDocumentWithPrompt(SolutionDialogOwner, saveAs: true), () => DocumentManager.CurrentSession is not null && !IsRunning);

            RunWorkflowCommand = new RelayCommand(async () => await ExecuteRunWorkflow(), () => !IsRunning && CanEditRootWorkspace && WorkflowTabViewModel.SelectedTab?.IsRootConfigurationReady == true);
            StopWorkflowCommand = new RelayCommand(ExecuteStopWorkflow, () => IsRunning);
            ShowSettingsCommand = new RelayCommand(ExecuteShowSettings);
            ShowAboutCommand = new RelayCommand(ExecuteShowAbout);
            ShowHelpCommand = new RelayCommand(ExecuteShowHelp);
            ShowShortcutsCommand = new RelayCommand(ExecuteShowShortcuts);
            PauseCommand = new RelayCommand(ExecutePause,CanPauseFormalExecution);
            UndoCommand = new RelayCommand(ExecuteUndo, CanExecuteUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, CanExecuteRedo);
            DeleteSelectedNodesCommand = new RelayCommand(ExecuteDeleteSelectedNodes, CanDeleteSelectedNodes);
            OpenDebugWindowCommand = new RelayCommand<NodeDefinition>(node => ExecuteOpenDebugWindow(node));
            TogglePathPointsCommand = new RelayCommand(ExecuteTogglePathPoints);

            // 所有工作流
            RunAllWorkflowsCommand = new RelayCommand(async () => await ExecuteRunAllWorkflows(), () => !IsRunning&&CanEditRootWorkspace&&WorkflowTabViewModel.Tabs.Any(tab=>tab.RootWorkflowNodes.Count>0));
            ToggleContinuousAllCommand = new RelayCommand(ExecuteToggleContinuousAll, CanToggleContinuousAll);

            // 图像视图

            // 图像操作
            ClearImageCommand = new RelayCommand(ExecuteClearImage);

            // 日志操作
            ClearLogCommand = new RelayCommand(ClearLog);

            // 解决方案管理

            // 管理器
            ShowGlobalVariableManagerCommand = new RelayCommand(ExecuteShowGlobalVariableManager);
            ShowCameraManagerCommand = new RelayCommand(ExecuteShowCameraManager);
            ShowCommunicationManagerCommand = new RelayCommand(ExecuteShowCommunicationManager, () => false);
            InitializeCommunicationAutomation();
            }
            catch { ReleaseDocumentHost(); throw; }
        }

        /// <summary>
        /// 获取当前选中工作流的独立执行上下文（按工作流ID懒加载）
        /// 每个工作流有独立的上下文，切换时互不干扰。
        /// </summary>


        /// <summary>
        /// 选中的标签页变化
        /// </summary>
        private void OnSelectedTabChanged(object? sender,EventArgs e)
        {
            ReleaseProductionEditorContexts(_productionEditors);ClearFormalImageSourceSelection();
            SelectedNode=null;SelectedConnection=null;
            var tab=WorkflowTabViewModel.SelectedTab;
            if(tab?.DocumentCanvas is {} canvas)
            {
                foreach(var node in canvas.Nodes)node.IsSelected=false;
                foreach(var connection in canvas.Connections)connection.IsSelected=false;
            }
            BindDocumentHistory();CloseExecutionSnapshotDetails();RefreshStatusBarExecutionSnapshot();
            UpdateCurrentCanvasType();UpdateUndoRedoCommands();
            OnPropertyChanged(nameof(CanEditWorkflowCanvas));OnPropertyChanged(nameof(CanEditRootWorkspace));OnPropertyChanged(nameof(CanEditRootCollection));
            SelectedTabChanged?.Invoke(this,EventArgs.Empty);
        }

        /// <summary>
        /// 工作流状态变化
        /// </summary>
        private void OnWorkflowStatusChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CanEditWorkflowCanvas));OnPropertyChanged(nameof(CanEditRootWorkspace));OnPropertyChanged(nameof(CanEditRootCollection));
            // 所有工作流状态
            IsAllWorkflowsRunning = WorkflowTabViewModel.IsAnyWorkflowRunning;
            AllWorkflowsRunButtonText = IsAllWorkflowsRunning ? "停止" : "运行";

            // 更新CanExecute状态
            (RunAllWorkflowsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ToggleContinuousAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 将 Tab 绑定到唯一的工作流定义集合。
        /// 新建 Tab 的节点可能先进入了 UI 临时集合，因此在首次绑定时迁移到引擎定义；
        /// 绑定完成后，后续节点和连接只能通过同一集合修改，避免 UI/Engine 分裂。
        /// </summary>


        /// <summary>
        /// Tab 重命名提交事件 — 同步更新引擎中对应 Workflow 的 Name
        /// </summary>
        private void OnTabsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(e.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Move)return;
            if(e.OldItems is not null)foreach(var tab in e.OldItems.OfType<WorkflowTabViewModel>())
            {tab.RenameRequested=null;_formalExecutionSnapshots.Remove(tab.RootWorkflowId);}
            if(e.NewItems is not null)foreach(var tab in e.NewItems.OfType<WorkflowTabViewModel>())
            {tab.RenameRequested=name=>RenameRootWorkspace(tab,name);EnsureFormalExecutionSnapshot(tab.RootWorkflowId);}
            OnPropertyChanged(nameof(WorkflowCount));NotifyRootCollectionAvailability();
        }
        /// <summary>
        /// 更新当前画布类型显示
        /// </summary>
        public void UpdateCurrentCanvasType()
        {
            if (WorkflowTabViewModel?.SelectedTab != null)
            {
                var canvasType = WorkflowTabViewModel.SelectedTab.CanvasType;
            CurrentCanvasTypeText = canvasType switch
            {
                SunEyeVision.UI.Features.WorkflowUI.Canvas.CanvasType.WorkflowCanvas => "工作流画布",
                _ => "未知类型"
            };
            }
            else
            {
                CurrentCanvasTypeText = "无画布";
            }
        }

        /// <summary>
        /// 订阅当前命令管理器的状态变化
        /// </summary>


        /// <summary>
        /// 更新撤销/重做CanExecute状态
        /// </summary>
        private void UpdateUndoRedoCommands()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var undoCmd = UndoCommand as RelayCommand;
                var redoCmd = RedoCommand as RelayCommand;
                undoCmd?.RaiseCanExecuteChanged();
                redoCmd?.RaiseCanExecuteChanged();

                // 更新状态显示

            });
        }

        /// <summary>
        /// 当前命令管理器状态变化
        /// </summary>


        /// <summary>
        /// 判断是否可以撤销当前选中的工作流
        /// </summary>
        private bool CanExecuteUndo()=>CanUndoRootWorkspace;

        /// <summary>
        /// 判断是否可以重做当前选中的工作流
        /// </summary>
        private bool CanExecuteRedo()=>CanRedoRootWorkspace;

        private void ExecutePause()
        {
            ToggleFormalPause();
        }

        private void ExecuteUndo() => ReplayDocumentEdit(undo: true);
        private void ExecuteRedo() => ReplayDocumentEdit(undo: false);
        private void ReplayDocumentEdit(bool undo)
        {
            if(!(undo?CanExecuteUndo():CanExecuteRedo()))return;
            try{var session=DocumentManager.CurrentSession!;if(undo)session.Undo();else session.Redo();RefreshRootTabReadiness();UpdateUndoRedoCommands();}
            catch(Exception error){StatusText="编辑历史回放失败："+error.Message;}
        }









        /// <summary>
        /// 保存当前解决方案
        /// </summary>
        private void ExecuteSaveCurrentSolution() => SaveCurrentDocumentWithPrompt(SolutionDialogOwner);



        /// <summary>
        /// 更新当前解决方案信息
        /// </summary>
        private bool _solutionInfoRefreshQueued;
        private void UpdateCurrentSolutionInfo()
        {
            // Visibility/retirement callbacks can run inside a document notification.
            // Read the settled session state after that owner-thread transaction returns.
            if (_solutionInfoRefreshQueued || _documentHostReleased) return;
            _solutionInfoRefreshQueued = true;
            var dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                _solutionInfoRefreshQueued = false;
                if (_documentHostReleased) return;
            OnPropertyChanged(nameof(CurrentSolutionName));
            OnPropertyChanged(nameof(CurrentSolutionPath));
            OnPropertyChanged(nameof(HasCurrentSolution));
            OnPropertyChanged(nameof(HasUnsavedSolutionChanges));
            var name = HasCurrentSolution ? CurrentSolutionName : "未打开方案";
            Title = "太阳眼视觉 - " + name + (HasUnsavedSolutionChanges ? " *" : "");
            }));
        }

        /// <summary>
        /// 当前解决方案变更事件处理
        /// </summary>


        /// <summary>
        /// 同步UI数据到Solution对象
        /// </summary>
        /// <param name="solutionManager">解决方案管理器</param>








        /// <summary>
        /// 为加载的工作流创建新的标签页
        /// </summary>
        /// <remarks>
        /// 优化说明（2026-03-26）：
        /// - 节点直接引用 Solution.Workflow.Nodes 中的对象
        /// - 不再克隆节点，消除 WorkflowTabInfo 中转层
        /// - UI 属性已在 WorkflowNodeBase 中定义
        /// - 优化说明（2026-03-30）：连接集合直接绑定，实现自动同步
        /// </remarks>


        /// <summary>
        /// 加载解决方案到工作流标签页
        /// </summary>
        /// <remarks>
        /// 加载流程（rule-003 日志系统使用规范）：
        /// 1. 验证解决方案和 WorkflowTabViewModel
        /// 2. 清空现有标签页
        /// 3. 遍历工作流，为每个工作流创建标签页
        /// 4. 在标签页创建过程中记录详细的加载日志
        /// 5. 统计和验证加载结果
        /// </remarks>


        internal bool CanNavigateLocalImages()=>ActiveImageSourceBinding?.CanActivate==true;
        internal System.Threading.Tasks.Task NavigateLocalImagesAsync(int direction)
        {
            if(direction is not (-1 or 1))throw new ArgumentOutOfRangeException(nameof(direction));
            var binding=ActiveImageSourceBinding;var source=binding?.Source;var session=DocumentManager.CurrentSession;var tab=WorkflowTabViewModel.SelectedTab;
            if(binding is null||source is null||session is null||tab is null||!CanNavigateLocalImages())return System.Threading.Tasks.Task.CompletedTask;
            var index=source.CurrentIndex+direction;if(index<0||index>=source.Count||!source.CanSelectImage(index))return System.Threading.Tasks.Task.CompletedTask;
            var root=session.GetWorkflows().Single(flow=>flow.Id==tab.RootWorkflowId);var node=session.GetNodes(root.Id).Single(item=>item.Id==source.NodeId);
            return ActivateFormalImageSourceAsync(new(session,root,node,source,source.Images[index],index,source.EditVersion));
        }
        public System.Threading.Tasks.Task RunSelectedImageAsync() => ExecuteRunWorkflow();

        private async System.Threading.Tasks.Task ExecuteRunWorkflow()
        {
            var tab=WorkflowTabViewModel.SelectedTab;
            if(tab is null||tab.IsInSubroutine){StatusText="只能运行当前实际根流程。";return;}
            try{await RunFormalWorkflowsAsync(new[]{tab.RootWorkflowId});}
            catch(OperationCanceledException){StatusText="正式运行已取消并排空。";}
            catch(Exception error){StatusText="正式运行失败："+error.Message;AddLog(StatusText);}
        }

        private void ExecuteStopWorkflow()
        {
            try { RequestStopFormalExecution(); StatusText="正在停止正式运行，等待当前调用排空。"; } catch(Exception error) { StatusText="停止请求失败，仍等待实际排空："+error.Message; }
        }

        private void ExecuteShowSettings()
        {
            // TODO: 显示设置对话框
        }

        private void ExecuteShowAbout()
        {
            var aboutWindow = new AboutWindow { Owner = System.Windows.Application.Current?.MainWindow };
            aboutWindow.ShowDialog();
        }

        private void ExecuteShowHelp()
        {
            var helpWindow = new HelpWindow();
            helpWindow.ShowDialog();
        }

        private void ExecuteShowShortcuts()
        {
            var helpWindow = new HelpWindow();
            helpWindow.ShowDialog();
            // TODO: 直接跳转到快捷键页面
        }

        #region 运行时参数注入

        /// <summary>
        /// 设置运行时参数
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="key">参数键名</param>
        /// <param name="value">参数值</param>


        /// <summary>
        /// 获取运行时参数
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="key">参数键名</param>
        /// <returns>参数值</returns>


        /// <summary>
        /// 获取所有运行时参数
        /// </summary>
        /// <returns>运行时参数字典</returns>


        /// <summary>
        /// 清除运行时参数
        /// </summary>


        /// <summary>
        /// 自动切换推进钩子：由图像预览宿主（ImageResultControl）注入。
        /// 在"新一轮工作流执行开始前"调用：开启自动切换时把运行时图像参数推进到下一张应运行图；
        /// 返回 true 表示已推进成功（本次 run 将使用新图）。
        /// </summary>
        #endregion

        /// <summary>
        /// 添加节点到当前工作流
        /// </summary>


        /// <summary>
        /// 添加连接到当前工作流（通过命令模式，支持连线覆盖）
        /// </summary>




        /// <summary>
        /// 检查节点是否受指定连接的影响（作为连接的 Source 或 Target）
        /// </summary>


        /// <summary>
        /// 如果当前选中节点受连接变更影响，强制刷新图像显示
        /// </summary>
        /// <remarks>
        /// 解决问题：连接节点后图像不立即更新，需切换节点再切回才显示最新结果
        /// </remarks>


        /// <summary>
        /// 连线建立后自动绑定 ImageSource — 确保所见即所得
        /// 同时对目标节点和源节点尝试自动绑定。
        /// 源节点绑定覆盖子工作流第一个节点（无上游连线）的场景。
        /// </summary>


        /// <summary>
        /// 连线删除仅清除它建立的默认图像绑定，保留显式来源，不自动换源。
        /// </summary>



        /// <summary>
        /// 清除所有节点选中状态
        /// </summary>
        private void ClearNodeSelections()
        {
            if (WorkflowTabViewModel.SelectedTab?.DocumentCanvas is not { } canvas)
                return;

            foreach (var node in canvas.Nodes)
            {
                node.IsSelected = false;
            }
        }

        /// <summary>
        /// 清除所有连接线选中状态
        /// </summary>
        private void ClearConnectionSelections()
        {
            if (WorkflowTabViewModel.SelectedTab?.DocumentCanvas is not { } canvas) return;
            foreach (var conn in canvas.Connections)
            {
                conn.IsSelected = false;
            }
        }

        /// <summary>
        /// 判断是否可以删除选中节点
        /// </summary>
        private bool CanDeleteSelectedNodes()
        {
            var canvas = WorkflowTabViewModel.SelectedTab?.DocumentCanvas;
            return CanEditWorkflowCanvas && canvas != null &&
                (canvas.Nodes.Any(node => node.IsSelected) || canvas.Connections.Any(edge => edge.IsSelected));
        }

        private void ExecuteDeleteSelectedNodes()
        {
            var canvas = WorkflowTabViewModel.SelectedTab?.DocumentCanvas;
            if (!CanEditWorkflowCanvas || canvas is null) return;
            var nodes = canvas.Nodes.Where(node => node.IsSelected).ToArray();
            var edges = canvas.Connections.Where(edge => edge.IsSelected).ToArray();
            if (nodes.Length == 0 && edges.Length == 0) return;
            var parts = new List<string>();
            if (nodes.Length > 0) parts.Add($"{nodes.Length} 个节点");
            if (edges.Length > 0) parts.Add($"{edges.Length} 条连接线");
            if (ThemeMessageDialog.Show($"确定要删除选中的 {string.Join("和 ", parts)} 吗？", "确认删除",
                MessageBoxButton.YesNo, ThemeMessageDialog.MessageIcon.Question, owner: null) != MessageBoxResult.Yes) return;
            try
            {
                canvas.Delete(nodes, edges);
                SelectedNode = null;
                SelectedConnection = null;
                AddLog($"已删除 {string.Join("和 ", parts)}");
            }
            catch (Exception error) { StatusText = "删除已取消：" + error.Message; }
        }
        // Keep selection identity and its epoch together; the setter retains all presentation work.
        private long _nodeSelectionIntent;
        internal bool SetSelectedNodeIdentity(NodeDefinition? node)
        {
            var intent=System.Threading.Interlocked.Increment(ref _nodeSelectionIntent);
            if (ReferenceEquals(_selectedNode, node)) return false;
            System.Threading.Interlocked.Increment(ref _outputContractSelectionGeneration);
            RetireResultPublication();
            if(intent!=_nodeSelectionIntent)return false;
            _declaredHistoricalResult=false;
            var changed=SetProperty(ref _selectedNode,node,propertyName:nameof(SelectedNode));
            return changed&&intent==_nodeSelectionIntent&&ReferenceEquals(_selectedNode,node);
        }

        private async void ExecuteOpenDebugWindow(NodeDefinition? node)
        {
            if (node is null) return;
            try { await OpenFormalNodeEditor(node); }
            catch (Exception error) { StatusText="节点打开失败："+error.Message; AddLog(StatusText); }
        }

        /// <summary>
        /// 控制流节点打开事件处理 - 由 NodeOpenDecisionChain 通过事件触发
        /// </summary>


        /// <summary>
        /// 打开或导航到子程序（VM风格：同一Tab内下钻，不新建Tab）
        /// 使用 SubroutineId 属性（而非 ExtraData）关联子程序工作流
        /// </summary>
        /// <param name="node">触发导航的子程序节点</param>


        /// <summary>
        /// 导航到指定工作流ID对应的画布内容
        /// 从引擎读取工作流的节点/连接并绑定到 Tab
        /// </summary>
        private void NavigateToWorkflowCanvas(string workflowId)
        {
            var session=DocumentManager.CurrentSession??throw new InvalidOperationException("没有当前方案。");
            var tab=WorkflowTabViewModel.SelectedTab??throw new InvalidOperationException("没有当前标签页。");
            if(tab.RootWorkflowId!=workflowId||!session.GetWorkflows().Any(flow=>flow.Id==workflowId&&!flow.IsSubroutine))throw new InvalidOperationException("导航目标不是标签页实际根流程。");
            if(tab.DocumentCanvas is null)tab.BindDocumentCanvas(session,workflowId);
            UpdateUndoRedoCommands();
        }

        /// <summary>
        /// 导航返回（由 [↩] 按钮触发）
        /// Tab 的 NavigateBack 已弹出工作流ID栈，此处加载对应画布内容
        /// </summary>


        /// <summary>
        /// 更新图像显示控件的数据源（当前节点 + 所有上游节点）
        /// 统一使用 DataSourceQueryService.GetImageDisplaySources() 获取数据源，
        /// 与 ImageSourceSelector 共享相同的元数据源
        /// </summary>


#if SUNEYE_GROUP_LEGACY
        /// <summary>
        /// 子程序输出增量更新入口：按变更类型对模块结果树 + 主界面图像源下拉做局部更新，
        /// 替代原来的"每字符全量重建"。
        /// - 结果树：仅当主界面当前选中该子程序节点时局部更新（结果树显示的是当前选中节点结果）；
        /// - 图像下拉：无条件局部更新（下拉含所有工作流的子程序图像输出，与选中节点无关）。
        /// </summary>
        public void ApplySubroutineOutputChange(
            SubroutineOutputChange change,
            string nodeName,
            string nodeId)
        {
            if (change?.Output == null) return;
            var cfg = change.Output;
            var isImage = string.Equals(cfg.TypeName, "Image", StringComparison.OrdinalIgnoreCase);
            var isPrevImage = string.Equals(change.PreviousTypeName, "Image", StringComparison.OrdinalIgnoreCase);
            var isSelectedSubroutine = _selectedNode?.Id == nodeId;

            // 1. 结果树局部更新：仅当主界面选中该子程序节点时
            if (isSelectedSubroutine&&_declaredResultActive)
            {
                if(!_declaredHistoricalResult)RefreshFormalPublishedResult();
            }
            else if (isSelectedSubroutine)
            {
                switch (change.ChangeType)
                {
                    case SubroutineOutputChangeType.Renamed:
                        ApplyRenameTree(change.PreviousName, cfg.Name, nodeId);
                        break;
                    case SubroutineOutputChangeType.TypeChanged:
                        ApplyTypeChangedTree(change.PreviousName, cfg.Name, isImage, cfg.Group, cfg.TypeName, nodeId);
                        break;
                    case SubroutineOutputChangeType.GroupChanged:
                        ApplyGroupChangedTree(change.PreviousName, cfg.Name, isImage, cfg.Group, cfg.TypeName, nodeId);
                        break;
                    case SubroutineOutputChangeType.Added:
                        AddLeafToTree(cfg.Name, isImage, cfg.Group, cfg.TypeName, nodeId);
                        break;
                    case SubroutineOutputChangeType.Removed:
                        ApplyRemoveFromTree(cfg.Name, nodeId);
                        break;
                }

                // 结构变化（类型/分组/增删）会影响结果树层级，需重新扁平化；纯改名只更新对应行。
                if (change.ChangeType != SubroutineOutputChangeType.Renamed)
                    BuildFlatRows();
            }

            // 2. 图像下拉局部更新：无条件（下拉可能含任意子程序的图像输出）
            ApplyImageSourceIncremental(change, isImage, isPrevImage, nodeId);
        }

        /// <summary>结果树改名：叶子原位更新显示名与属性路径</summary>
        private void ApplyRenameTree(string oldName, string newName, string nodeId)
        {
            if (string.IsNullOrEmpty(oldName)) return;
            var root = FindResultRoot(nodeId);
            if (root == null) return;
            var leaf = FindResultLeaf(root, nodeId, oldName);
            if (leaf == null) return;
            leaf.DisplayName = newName;
            leaf.PropertyName = newName;
            leaf.SourcePropertyName = newName;

            // UI 实际绑定 FlatResultRows（DataGrid），必须同步更新对应行，否则改名界面不刷新
            foreach (var row in FlatResultRows.Where(r => r.SourceNodeId == nodeId && r.SourcePropertyName == oldName))
            {
                row.DisplayName = newName;
                row.SourcePropertyName = newName;
            }
        }

        /// <summary>结果树类型变更：移除旧叶子并按新类型移动到正确分组</summary>
        private void ApplyTypeChangedTree(string oldName, string newName, bool isImage, string? group, string typeName, string nodeId)
        {
            if (string.IsNullOrEmpty(oldName)) return;
            ApplyRemoveFromTree(oldName, nodeId);
            AddLeafToTree(newName, isImage, group, typeName, nodeId);
        }

        /// <summary>结果树分组变更：非图像输出按新分组移动（图像固定归「图像」分组，不动）</summary>
        private void ApplyGroupChangedTree(string oldName, string newName, bool isImage, string? group, string typeName, string nodeId)
        {
            if (string.IsNullOrEmpty(oldName) || isImage) return;
            ApplyRemoveFromTree(oldName, nodeId);
            AddLeafToTree(newName, isImage, group, typeName, nodeId);
        }

        /// <summary>图像下拉局部更新：按变更类型做增删改（改名为移除旧条目+新增新名）</summary>
        private void ApplyImageSourceIncremental(SubroutineOutputChange change, bool isImage, bool isPrevImage, string nodeId)
        {
            var cfg = change.Output;
            switch (change.ChangeType)
            {
                case SubroutineOutputChangeType.Renamed:
                    if (isImage)
                    {
                        var oldItem = DisplayImageSources
                            .FirstOrDefault(s => s.SourceNodeId == nodeId && s.PropertyName == change.PreviousName);
                        if (oldItem != null)
                            DisplayImageSources.Remove(oldItem);
                        AddImageSourceItem(cfg.Name, nodeId);
                    }
                    break;
                case SubroutineOutputChangeType.TypeChanged:
                    if (isPrevImage)
                    {
                        var oldItem = DisplayImageSources
                            .FirstOrDefault(s => s.SourceNodeId == nodeId && s.PropertyName == change.PreviousName);
                        if (oldItem != null)
                            DisplayImageSources.Remove(oldItem);
                    }
                    if (isImage)
                        AddImageSourceItem(cfg.Name, nodeId);
                    break;
                case SubroutineOutputChangeType.Added:
                    if (isImage)
                        AddImageSourceItem(cfg.Name, nodeId);
                    break;
                case SubroutineOutputChangeType.Removed:
                    if (isImage)
                    {
                        var item = DisplayImageSources
                            .FirstOrDefault(s => s.SourceNodeId == nodeId && s.PropertyName == cfg.Name);
                        if (item != null)
                            DisplayImageSources.Remove(item);
                    }
                    break;
                case SubroutineOutputChangeType.GroupChanged:
                    // 分组字段不影响图像下拉条目（图像输出条目由类型决定，且下拉无分组列），忽略
                    break;
            }
        }

        /// <summary>在图像下拉追加一个子程序图像条目</summary>
        private void AddImageSourceItem(string name, string nodeId)
        {
            var nodeName = ResolveSubroutineNodeName(nodeId);
            var sysType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToSystemType("Image") ?? typeof(object);
            DisplayImageSources.Add(new AvailableDataSource
            {
                SourceNodeId = nodeId,
                SourceNodeName = nodeName,
                PropertyName = name,
                DisplayName = name,
                PropertyType = sysType,
                VariableType = "Image",
                GroupName = "图像",
                Description = $"子程序输出: {name}",
            });
        }

        /// <summary>解析子程序节点显示名（优先选中节点，其次引擎节点）</summary>
        private string ResolveSubroutineNodeName(string nodeId)
        {
            if (_selectedNode is {} selected && selected.Id == nodeId)
                return selected.Name ?? selected.DispName ?? "子程序";
            foreach (var wf in _workflowEngine.GetAllWorkflows())
            {
                var node = wf.Nodes.FirstOrDefault(n => n.Id == nodeId);
                if (node != null)
                    return node.Name ?? node.DispName ?? "子程序";
            }
            return "子程序";
        }

        /// <summary>定位结果树中该子程序的根节点（当前选中节点的结果根）</summary>
        private ModuleResultNode? FindResultRoot(string nodeId)
        {
            foreach (var root in ModuleResultTree)
            {
                // 根节点本身带 SourceNodeId 才需要匹配；子程序结果根由 UpdateModuleResults 生成时未设 SourceNodeId，
                // 因此直接匹配根节点的叶子归属：存在属于该 nodeId 的叶子即认为是该子程序的树。
                if (ContainsNodeId(root, nodeId))
                    return root;
            }
            return null;
        }

        private static bool ContainsNodeId(ModuleResultNode node, string nodeId)
        {
            if (node.SourceNodeId == nodeId) return true;
            foreach (var child in node.Children)
                if (ContainsNodeId(child, nodeId))
                    return true;
            return false;
        }

        /// <summary>递归查找结果树叶子（按 SourceNodeId + SourcePropertyName）</summary>
        private static ModuleResultNode? FindResultLeaf(ModuleResultNode node, string nodeId, string propertyName)
        {
            if (!node.IsGroup && node.SourceNodeId == nodeId && node.SourcePropertyName == propertyName)
                return node;
            foreach (var child in node.Children)
            {
                var found = FindResultLeaf(child, nodeId, propertyName);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>从结果树移除指定叶子，并清理空分组</summary>
        private void ApplyRemoveFromTree(string name, string nodeId)
        {
            var root = FindResultRoot(nodeId);
            if (root == null) return;
            RemoveResultLeaf(root, nodeId, name);
        }

        /// <summary>递归删除叶子；若父分组因此为空则一并移除，并递归向上清理空分组</summary>
        private static bool RemoveResultLeaf(ModuleResultNode node, string nodeId, string propertyName)
        {
            if (!node.IsGroup && node.SourceNodeId == nodeId && node.SourcePropertyName == propertyName)
                return true;

            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                var child = node.Children[i];
                if (RemoveResultLeaf(child, nodeId, propertyName))
                {
                    node.Children.RemoveAt(i);
                    // 分组删空后移除（递归向上由调用栈继续处理）
                    return node.IsGroup && node.Children.Count == 0;
                }
            }
            return false;
        }

        /// <summary>在结果树中按分组路径添加叶子（分组不存在则创建）</summary>
        private void AddLeafToTree(string name, bool isImage, string? group, string typeName, string nodeId)
        {
            var root = FindResultRoot(nodeId);
            if (root == null)
            {
                LogWarning($"子程序增量|未找到结果树根节点, 跳过新增: {name} (nodeId={nodeId})", "SubroutineIncremental");
                return;
            }

            var groupName = isImage ? "图像" : (group ?? "其他");
            var parent = EnsureGroupPath(root, groupName);
            var clrType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToSystemType(typeName) ?? typeof(object);

            parent.Children.Add(new ModuleResultNode
            {
                DisplayName = name,
                DisplayValue = "--",
                PropertyName = name,
                SourceNodeId = nodeId,
                SourcePropertyName = name,
                PropertyClrType = clrType,
                IsGroup = false
            });
        }

        /// <summary>确保分组路径存在，返回最终分组节点（支持 "a/b" 层级）</summary>
        private static ModuleResultNode EnsureGroupPath(ModuleResultNode root, string groupName)
        {
            var names = groupName.Split('/');
            ModuleResultNode current = root;
            var fullPath = string.Empty;
            foreach (var part in names)
            {
                fullPath = string.IsNullOrEmpty(fullPath) ? part : $"{fullPath}/{part}";
                var existing = current.Children.FirstOrDefault(c => c.IsGroup && c.DisplayName == part);
                if (existing == null)
                {
                    existing = new ModuleResultNode
                    {
                        DisplayName = part,
                        IsGroup = true,
                        IsExpanded = true
                    };
                    current.Children.Add(existing);
                }
                current = existing;
            }
            return current;
        }

#endif
        /// <summary>
        /// 延迟设置 SelectedDisplayImageSourceIndex，确保 WPF 先处理完 ItemsSource 的 CollectionChanged 事件
        /// （Clear + Add × N）后再设置 SelectedIndex，避免 ComboBox 内部因竞态条件选中项丢失
        /// 
        /// 使用两阶段策略：
        /// 原控件工作区自行维护稳定选择；这里只同步导航索引，
        /// 不再用临时-1触发清图，也不把列表重建当成用户选择写入方案。
        /// </summary>
        public void RefreshDisplayImageFromCurrentSource()
        {
            RefreshNativeResultImages();
        }

        /// <summary>
        /// 清空全部结果展示（实现 INodeResultSink）
        /// </summary>
        public void ClearResultDisplay()
        {
            var epoch=RetireResultPublication();_declaredHistoricalResult=false;
            if(epoch!=_resultPublicationEpoch)return;
            CalculationResults.Clear();
            if(epoch!=_resultPublicationEpoch)return;
            ModuleResultTree.Clear();
            if(epoch==_resultPublicationEpoch){HasModuleResults=false;ClearNativeImagePresentation();}
        }

        /// <summary>
        /// 更新数据分析控件 - 根据选中节点是否提供数据分析控件来显示/隐藏
        /// </summary>
        /// <param name="selectedNode">当前选中的节点</param>
        private IToolResultView? _activeResultView;
        private void ReleaseActiveResultView()
        {
            if(_activeResultView is not {} view)return;
            _activeResultView=null;
            view.SelectionHighlightChanged-=OnDeclaredSelectionHighlightChanged;
            view.Dispose();
        }

        private System.Threading.Tasks.Task UpdateDataAnalysisControl(NodeDefinition? selectedNode)
        {
            var owner=CaptureResultViewOwner(_resultPublicationEpoch);
            bool Current()=>owner()&&ReferenceEquals(SelectedNode,selectedNode);
            ReleaseActiveResultView();
            if(!Current())return System.Threading.Tasks.Task.CompletedTask;
            ClearResultHighlight();
            if(!Current())return System.Threading.Tasks.Task.CompletedTask;
            SelectedNodeDataAnalysisControl=null;
            if(!Current())return System.Threading.Tasks.Task.CompletedTask;
            HasDataAnalysisControl=false;
            if(!Current())return System.Threading.Tasks.Task.CompletedTask;
            DataAnalysisControlTitle="数据分析";
            if(selectedNode is null||!Current())return System.Threading.Tasks.Task.CompletedTask;
            var view=SunEyeVision.Plugin.Infrastructure.Managers.Plugin.DesktopToolPresentation.CreateCurrentResultView(DocumentManager.CurrentSession!.AdmitPresentation(WorkflowTabViewModel.SelectedTab!.CurrentWorkflowId,selectedNode.Id));
            if(!Current()){view?.Dispose();return System.Threading.Tasks.Task.CompletedTask;}
            if(view is null)
            {
                // Tools without the declared result companion have an honest unavailable entry.
                // Do not silently keep an SDK visual consumer outside the shared publication.
                DataAnalysisControlTitle=selectedNode.Name+" - 尚未提供声明式结果视图";
                return System.Threading.Tasks.Task.CompletedTask;
            }
            _activeResultView=view;view.SelectionHighlightChanged+=OnDeclaredSelectionHighlightChanged;
            DataAnalysisControlTitle=selectedNode.Name+" - 数据分析";
            if(!Current()||!ReferenceEquals(_activeResultView,view))return System.Threading.Tasks.Task.CompletedTask;
            SelectedNodeDataAnalysisControl=view.View;
            if(!Current()||!ReferenceEquals(_activeResultView,view))return System.Threading.Tasks.Task.CompletedTask;
            HasDataAnalysisControl=true;
            if(!Current()||!ReferenceEquals(_activeResultView,view))return System.Threading.Tasks.Task.CompletedTask;
            if(_resultPublication is {} publication&&publication.IsCurrent()&&Current())
                PresentResultPublication(publication,_resultPublicationSnapshot,selectedNode.Name,_resultPublicationBindings);
            else if(Current()&&ReferenceEquals(_activeResultView,view))view.Clear();
            return System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// 数据分析控件选中高亮联动 → 将高亮元素设置到图像渲染层
        /// </summary>
        private void OnDeclaredSelectionHighlightChanged(object? sender,ResultSelection selection)
        {
            var publication=_resultPublication;
            if(!ReferenceEquals(sender,_activeResultView)||publication is null||selection.Publication!=publication.Stamp
                ||!publication.IsCurrent()||!ReferenceEquals(_resultPublication,publication))return;
            ResultWorkspaceSelectionChanged?.Invoke(selection);
        }

        /// <summary>
        /// 获取节点的数据提供者（按 WorkflowId 缓存，SSOT 委托读取，抛弃旧的 _nodeResults 本地缓存）
        /// </summary>
        /// <param name="node">节点</param>
        /// <returns>数据提供者实例</returns>


        /// <summary>
        /// 为子程序工作流注册输入端口数据源提供者
        /// </summary>



        /// <summary>
        /// 在所有已注册工作流中查找引用指定子程序工作流 ID 的节点
        /// </summary>
        /// <param name="subroutineWorkflowId">子程序工作流 ID</param>
        /// <returns>父节点 ID，找不到则返回 null</returns>






        /// <summary>
        /// 通知所有已打开的调试窗口：上游节点已执行完成，可刷新运行时状态
        /// </summary>


        /// <summary>

        /// <summary>
        /// 在指定的 WorkflowCanvasControl 中切换显示包围矩形
        /// </summary>
        private void ToggleBoundingRectangleOnCanvas(WorkflowCanvasControl workflowCanvas)
        {
            workflowCanvas.ShowBoundingRectangle = !workflowCanvas.ShowBoundingRectangle;

            // 如果显示，使用第一个连接作为示例
            if (workflowCanvas.ShowBoundingRectangle)
            {
                var selectedTab = WorkflowTabViewModel?.SelectedTab;
                if (selectedTab != null && selectedTab.WorkflowConnections != null && selectedTab.WorkflowConnections.Count > 0)
                {
                    var firstConnection = selectedTab.WorkflowConnections.FirstOrDefault();
                    if (firstConnection != null)
                    {
                        workflowCanvas.BoundingSourceNodeId = firstConnection.SourceNodeId;
                        workflowCanvas.BoundingTargetNodeId = firstConnection.TargetNodeId;
                        AddLog($"[ToggleBoundingRectangle] 显示 {firstConnection.Id} 的包围矩形");
                        AddLog($"[ToggleBoundingRectangle]   源节点ID: {firstConnection.SourceNodeId}");
                        AddLog($"[ToggleBoundingRectangle]   目标节点ID: {firstConnection.TargetNodeId}");
                    }
                    else
                    {
                        AddLog("[ToggleBoundingRectangle] 未找到连接");
                        workflowCanvas.ShowBoundingRectangle = false;
                    }
                }
                else
                {
                    AddLog("[ToggleBoundingRectangle] 当前Tab没有连接");
                    workflowCanvas.ShowBoundingRectangle = false;
                }
            }

            AddLog($"[ToggleBoundingRectangle] ========== 包围矩形: {(workflowCanvas.ShowBoundingRectangle ? "显示" : "隐藏")} ==========");
        }

        /// <summary>
        /// 切换路径起点终点显示
        /// </summary>
        private void ExecuteTogglePathPoints()
        {
            AddLog("[TogglePathPoints] 切换显示路径起点终点");

            if (WorkflowTabViewModel?.SelectedTab?.WorkflowConnections != null)
            {
                var newState = !WorkflowTabViewModel.SelectedTab.WorkflowConnections.Any(c => c.ShowPathPoints);

                foreach (var connection in WorkflowTabViewModel.SelectedTab.WorkflowConnections)
                {
                    connection.ShowPathPoints = newState;
                }

                AddLog($"[TogglePathPoints] 显示路径起点终点: {(newState ? "显示" : "隐藏")}");
            }
        }

        /// <summary>
        /// 执行所有工作流
        /// </summary>
        private async System.Threading.Tasks.Task ExecuteRunAllWorkflows()
        {
            var session=DocumentManager.CurrentSession;if(session is null)return;
            try{await RunFormalWorkflowsAsync(session.GetWorkflows().Where(flow=>!flow.IsSubroutine).Select(flow=>flow.Id).ToArray());}
            catch(OperationCanceledException){StatusText="正式运行已取消并排空。";}
            catch(Exception error){StatusText="正式运行失败："+error.Message;AddLog(StatusText);}
        }

        /// <summary>
        /// 诊断设计期节点是否仍由 Solution/UI/Engine 共享同一对象。
        /// 只记录身份，不改变任何运行或保存逻辑。
        /// </summary>


        /// <summary>
        /// 切换所有工作流运行/停止
        /// </summary>
        private void ExecuteToggleContinuousAll() { if(IsContinuousRunning)ExecuteStopWorkflow(); else StartAllContinuousWorkflows(); }

        /// <summary>由预览 UI 发布当前输入；执行器只读取服务中的不可变选择快照。</summary>


        private bool StartContinuousWorkflow(WorkflowTabViewModel workflow) => StartFormalContinuous(new[]{workflow.RootWorkflowId});
        private bool StopContinuousWorkflow(WorkflowTabViewModel workflow) { if(!IsFormalRootRunning(workflow.RootWorkflowId))return false; RequestStopFormalExecution(); return true; }

        /// <summary>
        /// 执行开始事件
        /// </summary>


        /// <summary>连续执行每轮开始前：自动切换开启时推进输入图并刷新运行时参数，使本轮使用最新图源</summary>


        /// <summary>
        /// 执行完成事件
        /// </summary>


        /// <summary>
        /// 执行停止事件
        /// </summary>




        private void RefreshStatusBarExecutionSnapshot()
        {
            _statusBar.SetExecutionSnapshot(WorkflowTabViewModel.SelectedTab is {} tab ? EnsureFormalExecutionSnapshot(tab.RootWorkflowId) : null);
        }

        internal void CloseExecutionSnapshotDetails() => _executionSnapshotPresenter.Close();

        private void ShowExecutionSnapshot(Services.Workflow.WorkflowExecutionSnapshot? snapshot)
        {
            snapshot = WorkflowTabViewModel.SelectedTab is {} tab?EnsureFormalExecutionSnapshot(tab.RootWorkflowId):null;
            _statusBar.SetExecutionSnapshot(snapshot);
            _executionSnapshotPresenter.Show(snapshot, System.Windows.Application.Current?.MainWindow);
        }

        /// <summary>
        /// 执行错误事件
        /// </summary>


        /// <summary>
        /// 执行进度事件
        /// </summary>


        /// <summary>
        /// 节点执行完成事件 - 更新节点结果并刷新UI
        /// </summary>


        /// <summary>
        /// 节点执行完成后的后处理（DSQS 已通过 _ssotReader 委托自动读取 SSOT，无需推送）
        /// </summary>


        /// <summary>
        /// 查找指定类型的视觉子元素
        /// </summary>
        private static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
        {
            if (parent == null)
                return null;

            if (parent is T child)
                return child;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var found = FindVisualChild<T>(System.Windows.Media.VisualTreeHelper.GetChild(parent, i));
                if (found != null)
                    return found;
            }

            return null;
        }



        #region 图像操作

        /// <summary>
        /// 清除图像
        /// </summary>
        private void ExecuteClearImage()
        {
            try
            {
                ++_nativeExplicitClearEpoch;
                ClearNativeImagePresentation();
                AddLog("已清除图像");
            }
            catch (Exception ex)
            {
                AddLog($"清除图像失败: {ex.Message}");
            }
        }

        #endregion



        #region 结果更新

        /// <summary>
        /// 更新计算结果（支持字典形式）
        /// </summary>
        public void UpdateCalculationResults(Dictionary<string, object> results)
        {
            CalculationResults.Clear();

            if (results == null || results.Count == 0)
                return;

            foreach (var kvp in results)
            {
                CalculationResults.Add(new ResultItem
                {
                    Name = kvp.Key,
                    Value = kvp.Value?.ToString() ?? "null"
                });
            }

            AddLog($"添加了 {results.Count} 条结果记录");
        }

        /// <summary>
        /// 更新计算结果（支持 ResultItem 列表）
        /// </summary>
        public void UpdateCalculationResults(IReadOnlyList<SunEyeVision.Plugin.SDK.Execution.Results.ResultItem> resultItems)
        {
            CalculationResults.Clear();

            if (resultItems == null || resultItems.Count == 0)
                return;

            foreach (var item in resultItems)
            {
                string displayValue = FormatResultValue(item);
                CalculationResults.Add(new ResultItem
                {
                    Name = item.DisplayName ?? item.Name,
                    Value = displayValue
                });
            }
        }

        /// <summary>
        /// 更新模块结果（统一入口：有/无结果都用 _typeCache 驱动结构）
        /// </summary>
        /// <param name="result">运行结果（可为null，此时仅显示表头结构）</param>
        /// <param name="nodeName">节点显示名称</param>
        /// <param name="toolId">工具ID，用于从 _typeCache 查询属性元数据</param>
        /// <param name="nodeId">节点ID，用于绑定状态反查</param>
        /// <param name="parameters">工具参数（设计态时用于回退读取检测区/屏蔽区 RoI 结构）</param>
        private IReadOnlyDictionary<string,ResultBindingInfo> DeclaredBindings(SunEyeVision.Plugin.Contracts.ToolSchema schema,string? nodeId,string? workflowId=null)
        {
            var session=DocumentManager.CurrentSession;
            workflowId??=WorkflowTabViewModel?.SelectedTab?.CurrentWorkflowId;
            var roots=session?.Document.Workflows.Where(flow=>flow.Id==workflowId).ToArray();
            var root=roots is {Length:1}&&roots[0].Nodes.Count(node=>node.Id==nodeId)==1?roots[0]:null;
            return root is null||nodeId is null?new Dictionary<string,ResultBindingInfo>():schema.Outputs.ToDictionary(
                port=>port.Id,port=>new ResultBindingInfo(new SunEyeVision.Plugin.Contracts.OutputReference(nodeId,port.Id,port.Type,scopeId:root.Id,storage:port.Storage),port.Id,
                    session!.Document.GlobalVariables.Where(variable=>variable.ProducerBinding is not null)
                        .GroupBy(variable=>variable.ProducerBinding!.ReferenceExpression,StringComparer.Ordinal)
                        .ToDictionary(group=>group.Key,group=>string.Join("、",group.Select(variable=>variable.Name)),StringComparer.Ordinal)),StringComparer.Ordinal);
        }
        private void ShowDeclaredSnapshot(DeclaredOutputSnapshot snapshot,string? nodeName)=>PresentSnapshot(snapshot,nodeName);
        internal void ShowDeclaredRun(ExecutionResult run,SunEyeVision.Plugin.Contracts.RunIdentity identity,string? nodeName=null)
        {
            try
            {
                var snapshot=run.DeclaredOutputs.Single(item=>item.Identity==identity);
                if(!snapshot.IsComplete)throw new InvalidOperationException("This run has no complete declared output projection.");
                _declaredHistoricalResult=true;ShowDeclaredSnapshot(snapshot,nodeName);
            }
            catch{ClearResultDisplay();throw;}
        }
        public void CloseResultPresentation()
        {
            var epoch=RetireResultPublication();
            if(epoch!=_resultPublicationEpoch)return;
            ReleaseActiveResultView();
            if(epoch!=_resultPublicationEpoch)return;
            SelectedNodeDataAnalysisControl=null;
            if(epoch!=_resultPublicationEpoch)return;
            HasDataAnalysisControl=false;
            if(epoch!=_resultPublicationEpoch)return;
            var presenter=_declaredResultPresenter;_declaredResultPresenter=null;
            presenter?.Dispose();
            if(epoch!=_resultPublicationEpoch)return;
            _declaredResultActive=false;_declaredHistoricalResult=false;
        }
        internal void ReleasePluginConsumersForShutdown()
        {
            var errors=new List<Exception>();
            void Release(Action action){try{action();}catch(Exception error){errors.Add(error);}}
            Release(ReleaseProductionEditors);
            Release(CloseResultPresentation);

            Release(ClearResultDisplay);
            Release(ReleaseFormalExecution);

            if(errors.Count>0)throw new AggregateException("关闭时释放插件消费者失败。",errors);
        }
        public void UpdateModuleResults(DeclaredOutputSnapshot? result,string? nodeName,string? toolId=null,string? nodeId=null)
        {
            if(nodeId is not null&&SelectedNode?.Id!=nodeId)return;
            if(result is {IsComplete:true})PresentSnapshot(result,nodeName);else RefreshFormalPublishedResult();
        }

        /// <summary>
        /// 将 ModuleResultTree 扁平化为一维列表供 DataGrid 绑定
        /// </summary>
        public void BuildFlatRows()
        {
            if(_declaredResultActive){_declaredResultPresenter!.RefreshRows();return;}
            FlatResultRows.Clear();

            foreach (var rootNode in ModuleResultTree)
            {
                AddFlatRow(rootNode, 0);
            }
        }

        private void AddFlatRow(ModuleResultNode node, int level)
        {
            // 解析节点名称
            string? resolvedNodeName = null;
            if (node.SourceNodeId != null && WorkflowTabViewModel.SelectedTab != null)
            {
                var srcNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes
                    .FirstOrDefault(n => n.Id == node.SourceNodeId);
                if (srcNode != null)
                    resolvedNodeName = srcNode.Name;
            }

            var row = new FlatResultRow
            {
                DisplayName = node.DisplayName,
                DisplayValue = node.DisplayValue,
                Level = level,
                IsGroup = node.IsGroup,
                IsExpanded = node.IsExpanded,
                SourceNode = node,
                SourceNodeId = node.SourceNodeId,
                SourceNodeName = resolvedNodeName,
                SourcePropertyName = node.SourcePropertyName,
                PropertyClrType = node.PropertyClrType
                ,BindingInfo = node.BindingInfo
            };

            // 同步展开状态变化
            row.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FlatResultRow.IsExpanded) && node != null)
                {
                    node.IsExpanded = row.IsExpanded;
                    BuildFlatRows();
                }
            };

            FlatResultRows.Add(row);

            // 递归展开子节点
            if (node.IsGroup && node.IsExpanded)
            {
                foreach (var child in node.Children)
                {
                    AddFlatRow(child, level + 1);
                }
            }
        }
        public void ClearCalculationResults()
        {
            CalculationResults.Clear();
            AddLog("已清空信息面板");
        }

        /// <summary>
        /// 格式化结果项值
        /// </summary>
        private string FormatResultValue(SunEyeVision.Plugin.SDK.Execution.Results.ResultItem item)
        {
            if (item.Value == null) return "null";

            return item.Type switch
            {
                SunEyeVision.Plugin.SDK.Execution.Results.ResultItemType.Numeric => item.Unit != null
                    ? $"{item.Value} {item.Unit}"
                    : item.Value?.ToString() ?? "",
                SunEyeVision.Plugin.SDK.Execution.Results.ResultItemType.Boolean => (bool)item.Value ? "✓ 是" : "✗ 否",
                SunEyeVision.Plugin.SDK.Execution.Results.ResultItemType.Text => item.Value?.ToString() ?? "",
                _ => item.Value?.ToString() ?? ""
            };
        }

        #region 图像预览

        /// <summary>
        /// 图像预览显示状态管理
        /// </summary>
        /// <remarks>
        /// 规则:
        /// 1. 选择图像采集节点 -> 显示该节点的图像
        /// 2. 选择其他节点 -> BFS逆向追踪采集节点，找到可显示图像
        ///    (优化: 逆向追踪采集节点与当前显示同源则不更新)
        /// 3. 逆向追踪采集节点图像 -> 显示在图像预览中
        ///
        /// 重构说明:
        /// - 使用 InputSource（输入源）管理图像数据
        /// - 输入源存储用户选择的图像，执行流程不会修改
        ///
        /// 性能优化:
        /// - 只在状态真正变化时才更新属性，避免触发不必要的绑定刷新
        /// - 引用相等检测在 ActiveInputSource setter 中处理
        /// </remarks>






        private string _imagePreviewSourceMessage = string.Empty;
        public string ImagePreviewSourceMessage
        {
            get => _imagePreviewSourceMessage;
            private set => SetProperty(ref _imagePreviewSourceMessage, value);
        }

        /// <summary>
        /// 强制刷新图像预览（从尺寸变化等触发）
        /// 即使当前 SelectedNode 未改变，也重新检查是否显示图像预览
        /// </summary>


        #endregion

        #endregion // 显示

        #region 解决方案管理

        /// <summary>
        /// 显示解决方案配置界面
        /// </summary>
        private bool _solutionTransitionPrepared;
        private bool _transitionPreparationInProgress;
        internal bool CanAcceptExternalActivation => !_solutionTransitionPrepared && !_transitionPreparationInProgress
            && (System.Windows.Application.Current is not App app || !app.HasPendingWorkspaceDeviceRestore);
        public void EndSolutionTransition() { _solutionTransitionPrepared=false; ActiveImageSourceBinding?.RefreshAvailability(); OnPropertyChanged(nameof(CanEditWorkflowCanvas));OnPropertyChanged(nameof(CanEditRootWorkspace));OnPropertyChanged(nameof(CanEditRootCollection)); }
        public async System.Threading.Tasks.Task<bool> PrepareSolutionTransitionAsync(bool reload = false, Window? owner = null)
        {
            if (_transitionPreparationInProgress) return false;
            owner = ResolveSolutionDialogOwner(owner);
            _transitionPreparationInProgress = true;
            OnPropertyChanged(nameof(CanEditWorkflowCanvas));OnPropertyChanged(nameof(CanEditRootWorkspace));OnPropertyChanged(nameof(CanEditRootCollection));
            try
            {
                var manager = SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager;
                if (IsRunning &&
                    ThemeMessageDialog.Show("方案正在运行。停止全部执行后继续？", "切换方案", MessageBoxButton.OKCancel,
                        ThemeMessageDialog.MessageIcon.Question, owner) != MessageBoxResult.OK) return false;
                _solutionTransitionPrepared=true;
                await StopFormalExecutionAsync(TimeSpan.FromSeconds(30));
                if (manager.CurrentSession == null) return await TryCloseParameterWindowsForTransitionAsync();
                if (reload)
                {
                    var discard = ThemeMessageDialog.Show("从磁盘重新加载会放弃当前未保存的编辑，不会自动保存。是否继续？", "重新加载方案", MessageBoxButton.OKCancel, ThemeMessageDialog.MessageIcon.Question, owner);
                    if (discard != MessageBoxResult.OK) { EndSolutionTransition(); return false; }
                    return await TryCloseParameterWindowsForTransitionAsync();
                }
                // A transition prompt is not Apply/Save. Cancel must leave private drafts private.
                if (manager.CurrentSession?.HasUnsavedChanges!=true) return await TryCloseParameterWindowsForTransitionAsync();
                var result = ThemeMessageDialog.Show("当前方案有未保存修改。是否保存？", "保存方案", MessageBoxButton.YesNoCancel, ThemeMessageDialog.MessageIcon.Question, owner);
                if (result == MessageBoxResult.Cancel || result == MessageBoxResult.None) { EndSolutionTransition(); return false; }
                if (result == MessageBoxResult.Yes && (!SaveCurrentDocumentWithPrompt(owner) || manager.CurrentSession?.HasUnsavedChanges == true))
                { EndSolutionTransition(); return false; }
                return await TryCloseParameterWindowsForTransitionAsync();
            }
            catch (Exception ex)
            {
                EndSolutionTransition();
                ThemeMessageDialog.Show("操作未完成，原方案保持打开：" + ex.Message, "方案切换", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error, owner);
                return false;
            }
            finally { _transitionPreparationInProgress = false; ActiveImageSourceBinding?.RefreshAvailability(); OnPropertyChanged(nameof(CanEditWorkflowCanvas));OnPropertyChanged(nameof(CanEditRootWorkspace));OnPropertyChanged(nameof(CanEditRootCollection)); }
        }

        /// <summary>External file activation reuses existing stop/save and identity-conflict rules.</summary>
        internal Services.Solutions.WorkspaceOpenResult? LastWorkspaceOpenResult { get; private set; }
        // Detailed activation status also records failures after the actual session was installed.
        internal async Task<bool> OpenExternalSolutionAsync(string path,bool reload=false)
        {
            if(!CanAcceptExternalActivation)return false;
            var previous=DocumentManager.CurrentSession;LastWorkspaceOpenResult=null;
            try
            {
                var opened=await OpenFormalDocumentAsync(path,reload);
                var committed=!ReferenceEquals(previous,DocumentManager.CurrentSession);
                LastWorkspaceOpenResult=new(opened?(committed?Services.Solutions.WorkspaceOpenStatus.Opened:Services.Solutions.WorkspaceOpenStatus.ActivatedExisting):Services.Solutions.WorkspaceOpenStatus.RejectedBeforeCommit,committed);
                return opened;
            }
            catch(Exception error)
            {
                var committed=!ReferenceEquals(previous,DocumentManager.CurrentSession);
                LastWorkspaceOpenResult=new(committed?Services.Solutions.WorkspaceOpenStatus.CommittedDegraded:Services.Solutions.WorkspaceOpenStatus.RejectedBeforeCommit,committed,error.Message);
                StatusText=(committed?"方案已切换，部分激活未完成：":"方案打开未接纳：")+error.Message;AddLog(StatusText);return false;
            }
            finally{EndSolutionTransition();}
        }

        /// <summary>
        /// 显示全局变量管理器
        /// </summary>
        private void ExecuteShowGlobalVariableManager()
        {
            try
            {
                LogInfo("打开全局变量管理器");

                var solutionManager = SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager;
                var currentSolution = solutionManager.CurrentSession;

                if (currentSolution == null)
                {
                    ThemeMessageDialog.Show("请先打开一个解决方案", "提示",
                        MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Info);
                    return;
                }

                var session = DocumentManager.CurrentSession ?? throw new InvalidOperationException("当前没有方案编辑会话。");
                var viewModel = new SunEyeVision.UI.Features.GlobalVariableManager.GlobalVariableManagerViewModel(session,
                    () => !_documentHostReleased && ReferenceEquals(DocumentManager.CurrentSession, session),
                    () => !_documentHostReleased && !IsRunning && ReferenceEquals(DocumentManager.CurrentSession, session));
                var dialog = new SunEyeVision.UI.Features.GlobalVariableManager.GlobalVariableManagerDialog(viewModel);
                _variableEditors.Add(viewModel);
                dialog.Closed += (_, _) =>
                {
                    _variableEditors.Remove(viewModel);
                };

                dialog.Owner = System.Windows.Application.Current?.MainWindow;
                viewModel.SaveRequested = () => SaveCurrentDocumentWithPrompt(dialog);
                using var registration = TrackParameterWindow(dialog);
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                LogError($"打开全局变量管理器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 扁平化 ModuleResultTree（用于搜索节点）
        /// </summary>
        private static IEnumerable<ModuleResultNode> FlattenNodes(ModuleResultNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var sub in FlattenNodes(child))
                    yield return sub;
            }
        }

        /// <summary>
        /// 显示相机管理器
        /// </summary>
        private void ExecuteShowCameraManager()
        {
            try
            {
                LogInfo("打开相机管理器");

                var solutionManager = SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager;
                LogInfo($"SolutionManager: {solutionManager != null}");

                var currentSolution = solutionManager?.CurrentSession;
                LogInfo($"CurrentSolution: {currentSolution != null}");

                if (currentSolution == null)
                {
                    ThemeMessageDialog.Show("请先打开一个解决方案", "提示",
                        MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Info);
                    return;
                }

                LogInfo("创建 CameraManagerViewModel...");
                var viewModel = new SunEyeVision.UI.Features.Camera.CameraManagerViewModel(solutionManager);
                LogInfo($"ViewModel 已创建, 相机数量: {viewModel.Cameras?.Count ?? 0}");

                LogInfo("创建 CameraManagerDialog...");
                var dialog = new SunEyeVision.UI.Features.Camera.CameraManagerDialog(viewModel);

                LogInfo("显示对话框...");
                dialog.ShowDialog();

                LogInfo("对话框已关闭");
            }
            catch (Exception ex)
            {
                LogError($"打开相机管理器失败: {ex.Message}");
                LogError($"堆栈跟踪: {ex.StackTrace}");
                ThemeMessageDialog.Show($"打开相机管理器失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            }
        }

        /// <summary>
        /// 显示通讯管理器
        /// </summary>
        private void ExecuteShowCommunicationManager() => LogWarning("通信字段绑定及配置界面尚未迁移，当前不可用。");

        #endregion

    }

    /// <summary>
    /// 简单的 Action 命令实现
    /// </summary>
    public class ActionCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool>? _canExecute;

        public ActionCommand(Action<T> execute, Func<T, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || (parameter is T t && _canExecute(t));
        }

        public void Execute(object? parameter)
        {
            if (parameter is T t)
                _execute(t);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 模块结果树节点（分组可折叠显示）
    /// </summary>
    public class ModuleResultNode : ObservableObject
    {
        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 显示值（仅叶子节点有值）
        /// </summary>
        public string? DisplayValue { get; set; }

        /// <summary>
        /// CLR 属性名（用于运行后反射取值，仅叶子节点有值）
        /// </summary>
        public string? PropertyName { get; set; }

        /// <summary>
        /// 是否为分组节点（可展开/折叠）
        /// </summary>
        public bool IsGroup { get; set; }

        /// <summary>
        /// 是否展开
        /// </summary>
        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                    OnPropertyChanged(nameof(ExpandIcon));
            }
        }

        /// <summary>
        /// 子节点集合
        /// </summary>
        public ObservableCollection<ModuleResultNode> Children { get; } = new();

        /// <summary>
        /// 展开/折叠图标（仅分组显示）
        /// </summary>
        public string ExpandIcon => IsGroup ? (_isExpanded ? "▼ " : "▶ ") : "";

        /// <summary>关联的源工具节点ID（叶子节点使用时设置）</summary>
        public string? SourceNodeId { get; set; }

        /// <summary>关联的属性名完整路径（如 "阈值"）</summary>
        public string? SourcePropertyName { get; set; }

        /// <summary>关联的CLR属性类型（用于类型过滤）</summary>
        public Type? PropertyClrType { get; set; }

        /// <summary>绑定语义与展示层分离；目录节点为空，可展开图形节点也可拥有绑定信息。</summary>
        public ResultBindingInfo? BindingInfo { get; set; }
    }

    /// <summary>
    /// 扁平化结果行（用于 DataGrid 显示树形结构）
    /// </summary>
    public class FlatResultRow : ObservableObject
    {
        private readonly ObservableCollection<FlatResultRow> _children=new();
        public FlatResultRow()=>Children=new(_children);
        public ReadOnlyObservableCollection<FlatResultRow> Children {get;}
        internal ObservableCollection<FlatResultRow> MutableChildren=>_children;
        public string Key {get;internal set;}=string.Empty;
        private bool _isExpanded = true;
        private string _displayName = string.Empty;

        /// <summary>显示名称</summary>
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>显示值（仅叶子节点有值）</summary>
        public string? DisplayValue { get; set; }

        /// <summary>层级：0=根节点, 1=分组, 2=叶子</summary>
        public int Level { get; set; }

        /// <summary>是否为分组节点（可展开/折叠）</summary>
        public bool IsGroup { get; set; }

        /// <summary>是否展开</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                    OnPropertyChanged(nameof(ExpandIcon));
            }
        }

        /// <summary>展开/折叠图标</summary>
        public string ExpandIcon => IsGroup ? (_isExpanded ? "▼ " : "▶ ") : "";

        /// <summary>左侧缩进距离（每层 +24px，支持任意深度）</summary>
        public Thickness Indent => new Thickness(4 + Level * 24, Level == 0 ? 4 : 3, 0, Level == 0 ? 4 : 3);

        /// <summary>行背景色</summary>
        public Brush RowBackground => Level switch
        {
            0 => new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)),
            1 => new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFA)),
            2 => Brushes.White,
            _ => Brushes.White
        };

        /// <summary>字体粗细</summary>
        public FontWeight FontWeight => Level == 0 ? FontWeights.SemiBold : FontWeights.Normal;

        /// <summary>值列字体大小</summary>
        public double ValueFontSize => Level == 2 ? 11 : 12;

        /// <summary>关联的源树节点（用于展开/折叠时重建扁平列表）</summary>
        public ModuleResultNode? SourceNode { get; set; }

        /// <summary>当前结果项关联的工具节点ID</summary>
        public string? SourceNodeId { get; set; }

        /// <summary>当前结果项关联的工具节点名称</summary>
        public string? SourceNodeName { get; set; }

        /// <summary>当前结果项关联的属性名</summary>
        public string? SourcePropertyName { get; set; }

        /// <summary>关联的CLR属性类型（用于类型过滤）</summary>
        public Type? PropertyClrType { get; set; }
        private ResultBindingInfo? _bindingInfo;
        public ResultBindingInfo? BindingInfo
        {
            get=>_bindingInfo;
            set
            {
                if(!SetProperty(ref _bindingInfo,value))return;
                OnPropertyChanged(nameof(IsBindable));OnPropertyChanged(nameof(CanBindVariable));
                OnPropertyChanged(nameof(BindingUnavailableReason));
            }
        }
        public bool IsBindable => BindingInfo?.CanBind==true;
        public bool CanBindVariable=>IsBindable;
        private string? _bindingDisplayName;
        public string? BindingDisplayName {get=>_bindingDisplayName;internal set=>SetProperty(ref _bindingDisplayName,value);}
        public string BindingUnavailableReason=>IsBindable?string.Empty:"该项没有可承载的正式变量类型或资源存储；样本索引不支持绑定";
    }

    /// <summary>模块结果可绑定终点的运行时寻址信息。</summary>
    public sealed class ResultBindingInfo
    {
        public ResultBindingInfo(SunEyeVision.Plugin.Contracts.OutputReference reference,string resultPath,IReadOnlyDictionary<string,string>? bindingNames=null)
        {Reference=reference;ResultPath=resultPath;_referenceExpression=SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Serialize(reference);
            BindingNames=bindingNames is null?null:bindingNames is ReadOnlyDictionary<string,string>?bindingNames:new ReadOnlyDictionary<string,string>(bindingNames.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal));}
        public IReadOnlyDictionary<string,string>? BindingNames {get;}
        public string? BoundVariableNames=>BindingNames?.TryGetValue(ReferenceExpression,out var names)==true?names:null;
        public SunEyeVision.Plugin.Contracts.OutputReference Reference {get;}
        private readonly string _referenceExpression;
        public string ReferenceExpression=>_referenceExpression;
        public string SourceNodeId=>Reference.NodeId;
        public string ResultPath {get;}
        public bool CanBind
        {
            get
            {
                var type=Reference.Type;
                foreach(var member in Reference.MemberFieldPath)type=Project(type,member);
                var element=type.Shape==SunEyeVision.Plugin.Contracts.ValueShape.Sequence?type.Element!:type;
                if(element.Shape!=SunEyeVision.Plugin.Contracts.ValueShape.Scalar)return false;
                return SunEyeVision.Plugin.Contracts.VariableValueDeclarations.DisplayNames.Keys.SelectMany(name=>new[]{name,name+"[]"}).Any(vocabulary=>SunEyeVision.Plugin.Contracts.VariableValueDeclarations.TryGetPort(vocabulary,out var target)
                    &&target.Type.Shape==type.Shape&&(target.Type.Shape==SunEyeVision.Plugin.Contracts.ValueShape.Sequence?target.Type.Element:target.Type)==element);
            }
        }
        private static SunEyeVision.Plugin.Contracts.TypeRef Project(SunEyeVision.Plugin.Contracts.TypeRef type,string member)
        {
            if(type.Shape==SunEyeVision.Plugin.Contracts.ValueShape.Sequence)return SunEyeVision.Plugin.Contracts.TypeRef.SequenceOf(Project(type.Element!,member),type.Count);
            return type.Fields.Single(field=>field.Id==member).Type;
        }
    }

}

