using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using AppCommands = SunEyeVision.UI.Commands;
using SunEyeVision.UI.Models;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Workflow.Nodes;


using SunEyeVision.Plugin.SDK;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.UI;
using SunEyeVision.UI.Adapters;
using SunEyeVision.Workflow;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Factories;
using SunEyeVision.UI.Infrastructure;
using SunEyeVision.UI.Services.Workflow;
using SunEyeVision.UI.Views.Controls.Canvas;
using SunEyeVision.UI.Views.Controls.Panels;
using SunEyeVision.UI.Views.Windows;
using SunEyeVision.UI.Extensions;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Logic;
using SunEyeVision.UI.Converters.Path;
using SunEyeVision.UI.Services.Performance;
using SunEyeVision.UI.Services.Logging;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.UI.ViewModels
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
    public class MainWindowViewModel : ViewModelBase
    {
        private string _title = "太阳眼视觉";
        private bool _isRunning = false;
        private string _status = "";
        private string _currentCanvasTypeText = "原 Diagram (测试)";

        // 图像显示
        private BitmapSource? _displayImage;
        private double _imageScale = 1.0;

        private bool _showImagePreview = false;

        // 图像预览
        private bool _autoSwitchEnabled = true;
        private int _currentImageIndex = -1;

        // 参数选择状态（用于 TreeView 事件处理）
        private string? _selectedParameterName;

        // 所有工作流状态
        private bool _isAllWorkflowsRunning = false;
        private string _allWorkflowsRunButtonText = "运行";

        // 工作流文件管理
        private string? _currentWorkflowFilePath;
        private bool _isWorkflowModified = false;

        // 工作流引擎
        private readonly WorkflowEngine _workflowEngine;

        /// <summary>公开工作流引擎访问（供 MainWindow code-behind 等使用）</summary>
        public WorkflowEngine WorkflowEngine => _workflowEngine;
        private readonly ILogger _logger;

        // 执行管理
        private readonly WorkflowExecutionManager _executionManager;

        // 工作流上下文字典（按工作流ID索引，每个工作流独立存储执行结果）
        private readonly Dictionary<string, WorkflowContext> _workflowContexts = new();

        // 运行时参数存储
        private readonly Dictionary<string, object> _runtimeParameters = new();

        // 节点结果管理
        private readonly Services.Workflow.NodeResultManager _nodeResultManager;

        // 工作流执行协调器（图像切换优化）
        private WorkflowExecutionOrchestrator? _executionOrchestrator;

        // 已打开的调试窗口（全局单例）
        private Window? _openDebugWindow;

        /// <summary>
        /// 调试窗口是否打开（用于锁定画布操作，避免Delete键冲突）
        /// </summary>
        public bool IsDebugWindowOpen => _openDebugWindow != null && _openDebugWindow.IsLoaded;

        // 按 Workflow 缓存的 DSQS（key: WorkflowId，替代旧的按节点缓存的 _nodeDataProviders）
        private readonly Dictionary<string, DataSourceQueryService> _workflowDsqsCache = new();

        // 调试窗口缓存（用于在节点执行完成后通知 ToolDebugControl 刷新运行时状态）
        private readonly Dictionary<string, System.Windows.Window> _nodeDebugWindows = new();

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

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    Status = _isRunning ? "运行中" : "已停止";
                }
            }
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

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

        private WorkflowNodeBase? _selectedNode;
        private Models.ImageInputSource? _activeInputSource; // 活动输入源
        private string? _currentDisplayNodeId = null;  // 记录当前显示的采集节点ID，避免重复切换

        /// <summary>
        /// 当前活动输入源，用于绑定到图像预览控件
        /// 输入源存储用户选择的图像文件，执行流程不会修改
        /// </summary>
        public Models.ImageInputSource? ActiveInputSource
        {
            get => _activeInputSource;
            private set
            {
                if (ReferenceEquals(_activeInputSource, value))
                {
                    return;
                }
                SetProperty(ref _activeInputSource, value);
            }
        }

        /// <summary>
        /// 图像显示控件的数据源（当前节点 + 所有父节点）
        /// </summary>
        public ObservableCollection<AvailableDataSource> DisplayImageSources { get; }

        /// <summary>
        /// 当前选中的图像显示源索引
        /// </summary>
        private int _selectedDisplayImageSourceIndex = -1;

        /// <summary>
        /// 当前选中的图像显示源索引
        /// </summary>
        public int SelectedDisplayImageSourceIndex
        {
            get => _selectedDisplayImageSourceIndex;
            set
            {
                if (SetProperty(ref _selectedDisplayImageSourceIndex, value))
                {
                    var sourceInfo = (value >= 0 && value < DisplayImageSources.Count)
                        ? DisplayImageSources[value].ToString()
                        : "无效索引/空集合";
                    LogInfo($"图像源|SelectedIndex变更: {value} ({sourceInfo}), Count={DisplayImageSources.Count}", "ImageSourceSelector");
                    UpdateDisplayImageFromCurrentSource();
                }
            }
        }

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

        public WorkflowNodeBase? SelectedNode
        {
            get => _selectedNode;
            set
            {
                var oldNode = _selectedNode;
                bool changed = SetProperty(ref _selectedNode, value);

                if (changed)
                {
                    LogInfo($"图像源|节点切换: {(value != null ? $"{value.Name}({value.Id})" : "null")} ← {(oldNode != null ? $"{oldNode.Name}({oldNode.Id})" : "null")}", "ImageSourceSelector");

                    // 更新数据分析控件（同步等待异步操作）
                    UpdateDataAnalysisControl((Models.WorkflowNode?)value).GetAwaiter().GetResult();

                    // 先更新图像显示数据源（当前节点 + 父节点），RefreshResultDisplay 依赖正确的数据源
                    UpdateDisplayImageSources((Models.WorkflowNode?)value);

                    // 刷新结果显示（如果节点有缓存结果）
                    var executionResult = value != null ? CurrentWorkflowContext?.GetNodeResult(value.Id) : null;
                    var toolId = (value as WorkflowNodeBase)?.ToolType ?? "";

                    if (executionResult?.Result != null)
                    {
                        _nodeResultManager.RefreshResultDisplay((Models.WorkflowNode?)value, executionResult.Result);
                    }
                    else if (value != null)
                    {
                        // 未运行也从 _typeCache 显示属性表结构
                        ToolResults? tempResult = null;

                        // 动态工具：从 Parameters 输出契约构造临时结果（设计态）
                        if (value.Parameters is IOutputContractProvider provider)
                        {
                            var contract = provider.GetOutputContract();
                            if (contract.Count > 0)
                                tempResult = new ContractToolResults(contract);
                        }

                        UpdateModuleResults(tempResult, value.Name, toolId, value.Id, value.Parameters);

                        // 清空旧节点残留在图像上的图形覆盖，防止显示错位
                        _nodeResultManager.ClearResultGraphics();
                    }
                    else
                    {
                        // 清空结果显示
                        _nodeResultManager.ClearResultDisplay();
                    }

                    // 节点选中状态变化时更新图像预览
                    UpdateImagePreviewVisibility((Models.WorkflowNode?)value);
                }
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
        public void ForceSelectNode(WorkflowNodeBase node)
        {
            LogInfo($"图像源|ForceSelectNode: node={node?.Name}({node?.Id}), _selectedNode={_selectedNode?.Name}({_selectedNode?.Id})", "ImageSourceSelector");

            // 更新选中状态
            if (_selectedNode != node)
            {
                SelectedNode = node;
            }
            else
            {
                // ★ 优化：即使引用相同，也只刷新结果，不清空（防止闪烁）
                if (node?.LastResult != null)
                {
                    _nodeResultManager.RefreshResultDisplay((Models.WorkflowNode?)node, (Plugin.SDK.Execution.Results.ToolResults?)node.LastResult);
                }
                UpdateDisplayImageSources((Models.WorkflowNode?)node);
                UpdateImagePreviewVisibility((Models.WorkflowNode?)node);
            }
        }

        public WorkflowConnection? SelectedConnection { get; set; }

        // 工作流标签页
        public WorkflowTabControlViewModel WorkflowTabViewModel { get; }

        /// <summary>
        /// 当前选中的工作流标签页的命令管理器
        /// 每个标签页拥有独立的命令管理器
        /// </summary>
        public AppCommands.CommandManager? CurrentCommandManager
        {
            get => WorkflowTabViewModel.SelectedTab?.CommandManager;
        }

        // 用于跟踪当前已订阅的命令管理器
        private AppCommands.CommandManager? _subscribedCommandManager;

        public string StatusText
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        // 当前解决方案信息
        private string _currentSolutionName = string.Empty;
        private string _currentSolutionPath = string.Empty;

        /// <summary>
        /// 当前解决方案名称
        /// </summary>
        public string CurrentSolutionName
        {
            get => _currentSolutionName;
            set => SetProperty(ref _currentSolutionName, value);
        }

        /// <summary>
        /// 当前解决方案路径
        /// </summary>
        public string CurrentSolutionPath
        {
            get => _currentSolutionPath;
            set => SetProperty(ref _currentSolutionPath, value);
        }

        /// <summary>
        /// 是否有当前解决方案
        /// </summary>
        public bool HasCurrentSolution => !string.IsNullOrEmpty(CurrentSolutionName);

        public string CameraStatus => "相机(2台)";

        // 图像显示相关
        public BitmapSource? DisplayImage
        {
            get => _displayImage;
            set => SetProperty(ref _displayImage, value);
        }

        public double ImageScale
        {
            get => _imageScale;
            set
            {
                if (SetProperty(ref _imageScale, value))
                {
                    OnPropertyChanged(nameof(DisplayImage));
                }
            }
        }


        /// <summary>
        /// 是否显示图像及其预览模块（ImageCaptureTool节点专用）
        /// </summary>
        public bool ShowImagePreview
        {
            get => _showImagePreview;
            set
            {
if (SetProperty(ref _showImagePreview, value))
                {
OnPropertyChanged(nameof(ImagePreviewHeight));
                }
            }
        }

        /// <summary>
        /// 图像预览高度，用于动态控制图像预览模块的空间
        /// </summary>
        public GridLength ImagePreviewHeight
        {
            get => ShowImagePreview ? new GridLength(60) : new GridLength(0);
        }

        /// <summary>
        /// 计算结果
        /// </summary>
        public ObservableCollection<ResultItem> CalculationResults { get; }

        /// <summary>
        /// 模块结果树（分组可折叠显示）
        /// </summary>
        public ObservableCollection<ModuleResultNode> ModuleResultTree { get; } = new();

        /// <summary>
        /// 扁平化结果行列表（用于 DataGrid 绑定）
        /// </summary>
        public ObservableCollection<FlatResultRow> FlatResultRows { get; } = new();

        /// <summary>
        /// 是否有模块结果（用于空状态显示）
        /// </summary>
        public bool HasModuleResults
        {
            get => _hasModuleResults;
            internal set => SetProperty(ref _hasModuleResults, value);
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
        /// 图像集合，使用优化集合
        /// </summary>
        public BatchObservableCollection<ImageInfo> ImageCollection { get; }

        /// <summary>
        /// 是否自动切换
        /// </summary>
        public bool AutoSwitchEnabled
        {
            get => _autoSwitchEnabled;
            set => SetProperty(ref _autoSwitchEnabled, value);
        }

        /// <summary>
        /// 当前显示图像索引
        /// </summary>
        public int CurrentImageIndex
        {
            get => _currentImageIndex;
            set
            {
                if (SetProperty(ref _currentImageIndex, value))
                {
                    UpdateCurrentImageDisplay();
                }
            }
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

        /// <summary>
        /// 获取工作流执行协调器
        /// </summary>
        public WorkflowExecutionOrchestrator? ExecutionOrchestrator => _executionOrchestrator;

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public (int Count, long MemoryBytes, double MemoryMB) GetCacheStatistics()
        {
            return _executionOrchestrator?.GetCacheStatistics() ?? (0, 0, 0);
        }

        /// <summary>
        /// 清空执行缓存
        /// </summary>
        public void ClearExecutionCache()
        {
            _executionOrchestrator?.ClearCache();
            AddLog("🗑️ 已清空执行缓存");
        }

        public ICommand NewWorkflowCommand { get; }
        public ICommand OpenWorkflowCommand { get; }
        public ICommand SaveWorkflowCommand { get; }
        public ICommand SaveAsWorkflowCommand { get; }

        // 解决方案保存命令
        public ICommand SaveCurrentSolutionCommand { get; }

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
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand FitToWindowCommand { get; }
        public ICommand ResetViewCommand { get; }
        public ICommand ToggleFullScreenCommand { get; }

        // 图像操作
        public ICommand ClearImageCommand { get; }

        public ICommand ClearLogCommand { get; }

        // 解决方案管理
        public ICommand ShowSolutionConfigurationCommand { get; }

        // 管理器
        public ICommand ShowGlobalVariableManagerCommand { get; }
        public ICommand ShowCameraManagerCommand { get; }
        public ICommand ShowCommunicationManagerCommand { get; }

        // 主窗口 ImageControl 获取委托 - 用于区域编辑器绑定
        /// <summary>
        /// 获取主窗口 ImageControl 的委托 - 用于区域编辑器绑定
        /// </summary>
        /// <remarks>
        /// 由 MainWindow 在初始化时设置，用于在调试窗口创建时传递 ImageControl 引用
        /// </remarks>
        public Func<ImageControl?>? GetMainImageControl { get; set; }

        public MainWindowViewModel()
        {
            Toolbox = new ToolboxViewModel();

            WorkflowTabViewModel = new WorkflowTabControlViewModel();

            // 初始化计算结果
            CalculationResults = new ObservableCollection<ResultItem>();

            // 初始化图像集合（优化版）
            ImageCollection = new BatchObservableCollection<ImageInfo>();

            // 初始化图像显示数据源
            DisplayImageSources = new ObservableCollection<AvailableDataSource>();

            // 初始化工作流引擎
            _logger = VisionLogger.Instance;
            _workflowEngine = new WorkflowEngine(_logger);

            // 初始化执行管理器（传入共享的 WorkflowEngine，子程序工作流已注册在其中）
            _executionManager = new Services.Workflow.WorkflowExecutionManager(new Infrastructure.DefaultInputProvider(), _workflowEngine);

            // 初始化节点结果管理器
            _nodeResultManager = new Services.Workflow.NodeResultManager(() => CurrentWorkflowContext, this);

            // 初始化工作流执行协调器（图像切换优化）
            _executionOrchestrator = new WorkflowExecutionOrchestrator(_nodeResultManager, maxCacheSize: 50, maxMemoryMB: 100);

            // 订阅执行管理器事件
            _executionManager.WorkflowExecutionStarted += OnWorkflowExecutionStarted;
            _executionManager.WorkflowExecutionCompleted += OnWorkflowExecutionCompleted;
            _executionManager.WorkflowExecutionStopped += OnWorkflowExecutionStopped;
            _executionManager.WorkflowExecutionError += OnWorkflowExecutionError;
            _executionManager.WorkflowExecutionProgress += OnWorkflowExecutionProgress;
            _executionManager.NodeExecutionCompleted += OnNodeExecutionCompleted;

            // 初始化当前画布类型
            UpdateCurrentCanvasType();

            // 选中标签页变化时更新运行/停止按钮状态
            WorkflowTabViewModel.SelectionChanged += OnSelectedTabChanged;

            // 监听新 Tab 创建，自动订阅 NameCommitted 事件
            WorkflowTabViewModel.Tabs.CollectionChanged += OnTabsCollectionChanged;

            // 为已存在的 Tab（如默认工作流）订阅 NameCommitted
            foreach (var tab in WorkflowTabViewModel.Tabs)
            {
                tab.NameCommitted -= OnTabNameCommitted;
                tab.NameCommitted += OnTabNameCommitted;
            }

            // 订阅工作流状态变化
            WorkflowTabViewModel.WorkflowStatusChanged += OnWorkflowStatusChanged;

            // 订阅工作流添加事件（注册引擎 + 同步 solution.Workflows）
            WorkflowTabViewModel.WorkflowAdded += OnWorkflowAdded;

            // Phase 3: 订阅工作流删除事件（清理引擎和 Solution）
            WorkflowTabViewModel.WorkflowDeleted += OnWorkflowDeleted;

            // 订阅控制流节点打开事件（由 NodeOpenDecisionChain 统一路由）
            NodeOpenDecisionChain.OnControlFlowNodeOpen += OnControlFlowNodeOpen;

            // 命令管理器的初始化
            SubscribeToCurrentCommandManager();

            // 监听解决方案变更事件
            var solutionManager = Adapters.ServiceInitializer.SolutionManager;
            solutionManager.CurrentSolutionChanged += OnCurrentSolutionChanged;

            // 初始化当前解决方案信息
            UpdateCurrentSolutionInfo();

            NewWorkflowCommand = new RelayCommand(ExecuteNewWorkflow);
            OpenWorkflowCommand = new RelayCommand(ExecuteOpenWorkflow);
            SaveWorkflowCommand = new RelayCommand(ExecuteSaveWorkflow);
            SaveAsWorkflowCommand = new RelayCommand(ExecuteSaveAsWorkflow);

            // 解决方案保存命令
            SaveCurrentSolutionCommand = new RelayCommand(ExecuteSaveCurrentSolution);

            RunWorkflowCommand = new RelayCommand(async () => await ExecuteRunWorkflow(), () => !IsRunning);
            StopWorkflowCommand = new RelayCommand(ExecuteStopWorkflow, () => IsRunning);
            ShowSettingsCommand = new RelayCommand(ExecuteShowSettings);
            ShowAboutCommand = new RelayCommand(ExecuteShowAbout);
            ShowHelpCommand = new RelayCommand(ExecuteShowHelp);
            ShowShortcutsCommand = new RelayCommand(ExecuteShowShortcuts);
            PauseCommand = new RelayCommand(ExecutePause);
            UndoCommand = new RelayCommand(ExecuteUndo, CanExecuteUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, CanExecuteRedo);
            DeleteSelectedNodesCommand = new RelayCommand(ExecuteDeleteSelectedNodes, CanDeleteSelectedNodes);
            OpenDebugWindowCommand = new RelayCommand<Models.WorkflowNode>(node => ExecuteOpenDebugWindow(node));
            TogglePathPointsCommand = new RelayCommand(ExecuteTogglePathPoints);

            // 所有工作流
            RunAllWorkflowsCommand = new RelayCommand(async () => await ExecuteRunAllWorkflows(), () => !IsAllWorkflowsRunning);
            ToggleContinuousAllCommand = new RelayCommand(ExecuteToggleContinuousAll, () => true);

            // 图像视图
            ZoomInCommand = new RelayCommand(ExecuteZoomIn);
            ZoomOutCommand = new RelayCommand(ExecuteZoomOut);
            FitToWindowCommand = new RelayCommand(ExecuteFitToWindow);
            ResetViewCommand = new RelayCommand(ExecuteResetView);
            ToggleFullScreenCommand = new RelayCommand(ExecuteToggleFullScreen);

            // 图像操作
            ClearImageCommand = new RelayCommand(ExecuteClearImage);

            // 日志操作
            ClearLogCommand = new RelayCommand(ClearLog);

            // 解决方案管理
            ShowSolutionConfigurationCommand = new RelayCommand(ExecuteShowSolutionConfiguration);

            // 管理器
            ShowGlobalVariableManagerCommand = new RelayCommand(ExecuteShowGlobalVariableManager);
            ShowCameraManagerCommand = new RelayCommand(ExecuteShowCameraManager);
            ShowCommunicationManagerCommand = new RelayCommand(ExecuteShowCommunicationManager);
        }

        /// <summary>
        /// 获取当前选中工作流的独立执行上下文（按工作流ID懒加载）
        /// 每个工作流有独立的上下文，切换时互不干扰。
        /// </summary>
        private WorkflowContext? CurrentWorkflowContext
        {
            get
            {
                var selectedTab = WorkflowTabViewModel?.SelectedTab;
                if (selectedTab == null) return null;

                if (!_workflowContexts.TryGetValue(selectedTab.Id, out var ctx))
                {
                    ctx = new WorkflowContext
                    {
                        WorkflowId = selectedTab.Id,
                        WorkflowName = selectedTab.Name,
                        ExecutionStartTime = DateTime.Now
                    };
                    _workflowContexts[selectedTab.Id] = ctx;
                }
                return ctx;
            }
        }

        /// <summary>
        /// 选中的标签页变化
        /// </summary>
        private void OnSelectedTabChanged(object? sender, EventArgs e)
        {
            // 更新命令管理器
            SubscribeToCurrentCommandManager();

            // 更新撤销/重做按钮状态
            UpdateUndoRedoCommands();

            // 更新当前显示
            UpdateCurrentCanvasType();

            // 更新 SmartPathConverter 的节点集合
            if (WorkflowTabViewModel?.SelectedTab != null)
            {
                Converters.Path.SmartPathConverter.Nodes = WorkflowTabViewModel.SelectedTab.WorkflowNodes;
                Converters.Path.SmartPathConverter.Connections = WorkflowTabViewModel.SelectedTab.WorkflowConnections;
            }

            // Fix: Set WorkflowEngine.CurrentWorkflow
            if (_workflowEngine != null && WorkflowTabViewModel?.SelectedTab != null)
            {
                var selectedId = WorkflowTabViewModel.SelectedTab.Id;
                var selectedName = WorkflowTabViewModel.SelectedTab.Name;

                // 如果引擎中尚未注册该工作流，自动创建（例如新建 Tab 时）
                if (_workflowEngine.GetWorkflowById(selectedId) == null)
                {
                    _workflowEngine.CreateWorkflow(selectedId, selectedName);
                    _logger?.LogInfo($"OnSelectedTabChanged: Auto-created workflow in engine: {selectedName} (ID: {selectedId})");
                }

                // 设置当前工作流（引擎是SSOT，无需额外同步）
                _workflowEngine.SetCurrentWorkflow(selectedId);
                _logger?.LogInfo($"OnSelectedTabChanged: Current workflow set to {selectedName} (ID: {selectedId})");
            }

            // ★ 切换标签页时清除选中状态（避免旧标签页的选中节点引用残留）
            SelectedNode = null;
            SelectedConnection = null;
            ClearNodeSelections();
            ClearConnectionSelections();
        }

        /// <summary>
        /// 工作流状态变化
        /// </summary>
        private void OnWorkflowStatusChanged(object? sender, EventArgs e)
        {
            // 所有工作流状态
            IsAllWorkflowsRunning = WorkflowTabViewModel.IsAnyWorkflowRunning;
            AllWorkflowsRunButtonText = IsAllWorkflowsRunning ? "停止" : "运行";

            // 更新CanExecute状态
            (RunAllWorkflowsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ToggleContinuousAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 工作流删除事件处理（Phase 3: 清理引擎和 Solution 中的对应工作流 + DSQS 缓存）
        /// </summary>
        /// <summary>
        /// 工作流添加事件处理 — 统一注册引擎 + 同步 solution.Workflows
        /// </summary>
        /// <remarks>
        /// 由 WorkflowTabViewModel.AddWorkflow 触发。
        /// 解决历史 Bug：UI 按钮 / Ctrl+N 添加工作流后，solution.Workflows 未更新，
        /// 导致全局变量对话框等只读 solution.Workflows 的 UI 看不到新工作流。
        /// </remarks>
        private void OnWorkflowAdded(object? sender, WorkflowTabViewModel addedTab)
        {
            if (addedTab == null)
                return;

            // 1. 引擎内复用同一实例（idempotent：OnSelectedTabChanged 通常已先创建）
            //    引擎没有时才创建。后续 Solution 与 Engine 共享同一 Workflow 引用，
            //    避免双实例漂移（节点加在引擎实例、Solution 实例始终为空）。
            var engineWorkflow = _workflowEngine.GetWorkflowById(addedTab.Id);
            if (engineWorkflow == null)
            {
                engineWorkflow = _workflowEngine.CreateWorkflow(addedTab.Id, addedTab.Name);
            }
            else if (engineWorkflow.Name != addedTab.Name)
            {
                // 同步名称（用户在 Tab 上改名时引擎实例可能先于事件到达）
                engineWorkflow.Name = addedTab.Name;
            }

            // 2. 共享同一实例到 solution.Workflows（不再 new 一个新 Workflow）
            SyncWorkflowToSolution(engineWorkflow, addedTab.Name);
        }

        /// <summary>
        /// 将工作流实例同步到 Solution（确保运行时目标参数选择树可以看到工作流中的工具参数）。
        /// 统一入口 — OnWorkflowAdded 和 CreateNewSubroutineWorkflow 都调用此方法，
        /// 避免重复代码，且确保所有创建路径都经过同一同步点。
        /// </summary>
        private void SyncWorkflowToSolution(SunEyeVision.Workflow.Workflow workflow, string? displayName = null)
        {
            if (workflow == null) return;
            var solutionManager = Adapters.ServiceInitializer.SolutionManager;
            var solution = solutionManager?.CurrentSolution;
            if (solution != null && !solution.Workflows.Any(w => w.Id == workflow.Id))
            {
                solution.Workflows.Add(workflow);
                var name = displayName ?? workflow.Name;
                _logger?.LogInfo(
                    $"SyncWorkflowToSolution: {name} (ID: {workflow.Id}, IsSubroutine: {workflow.IsSubroutine})");
            }
        }

        private void OnWorkflowDeleted(object? sender, WorkflowTabViewModel deletedTab)
        {
            if (deletedTab == null)
                return;

            // 1. 从引擎中移除
            _workflowEngine.DeleteWorkflow(deletedTab.Id);

            // 2. 从 Solution 中移除
            var solutionManager = Adapters.ServiceInitializer.SolutionManager;
            if (solutionManager?.CurrentSolution != null)
            {
                solutionManager.CurrentSolution.RemoveWorkflow(deletedTab.Id);
                LogSuccess($"工作流删除事件处理：已从引擎和解决方案中移除工作流: {deletedTab.Name}");
            }

            // 3. 清理按 Workflow 缓存的 DSQS
            _workflowDsqsCache.Remove(deletedTab.Id);
        }

        /// <summary>
        /// Tab 重命名提交事件 — 同步更新引擎中对应 Workflow 的 Name
        /// </summary>
        private void OnTabNameCommitted(object? sender, string newName)
        {
            if (sender is not WorkflowTabViewModel tab) return;

            // 更新引擎中当前显示的工作流名称
            var engineWorkflow = _workflowEngine.GetWorkflow(tab.CurrentWorkflowId);
            if (engineWorkflow != null && engineWorkflow.Name != newName)
            {
                engineWorkflow.Name = newName;
                LogInfo($"引擎工作流已重命名: {engineWorkflow.Name} (ID: {tab.CurrentWorkflowId})");
            }
        }

        /// <summary>
        /// Tabs 集合变化事件 — 为新增的 Tab 订阅 NameCommitted
        /// </summary>
        private void OnTabsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is WorkflowTabViewModel tab)
                    {
                        tab.NameCommitted -= OnTabNameCommitted;
                        tab.NameCommitted += OnTabNameCommitted;
                    }
                }
            }
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
                Views.Controls.Canvas.CanvasType.WorkflowCanvas => "工作流画布",
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
        private void SubscribeToCurrentCommandManager()
        {
            // 取消订阅旧的命令管理器
            if (_subscribedCommandManager != null)
            {
                _subscribedCommandManager.CommandStateChanged -= OnCurrentCommandManagerStateChanged;
            }

            // 订阅新的命令管理器
            if (CurrentCommandManager != null)
            {
                CurrentCommandManager.CommandStateChanged += OnCurrentCommandManagerStateChanged;
                _subscribedCommandManager = CurrentCommandManager;
            }
            else
            {
                _subscribedCommandManager = null;
            }
        }

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
                StatusText = CurrentCommandManager?.LastCommandDescription ?? "";
            });
        }

        /// <summary>
        /// 当前命令管理器状态变化
        /// </summary>
        private void OnCurrentCommandManagerStateChanged(object? sender, EventArgs e)
        {
            UpdateUndoRedoCommands();
        }

        /// <summary>
        /// 判断是否可以撤销当前选中的工作流
        /// </summary>
        private bool CanExecuteUndo()
        {
            return CurrentCommandManager?.CanUndo ?? false;
        }

        /// <summary>
        /// 判断是否可以重做当前选中的工作流
        /// </summary>
        private bool CanExecuteRedo()
        {
            return CurrentCommandManager?.CanRedo ?? false;
        }

        private void ExecutePause()
        {
            // TODO: 实现暂停
        }

        private void ExecuteUndo()
        {
            if (CurrentCommandManager == null)
            {
                AddLog("❌ 没有选中的工作流，无法撤销");
                return;
            }

            try
            {
                CurrentCommandManager.Undo();
                AddLog($"↩️ 撤销: {CurrentCommandManager.LastCommandDescription}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"撤销失败: {ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteRedo()
        {
            if (CurrentCommandManager == null)
            {
                AddLog("❌ 没有选中的工作流，无法重做");
                return;
            }

            try
            {
                CurrentCommandManager.Redo();
                AddLog($"↪️ 重做: {CurrentCommandManager.LastCommandDescription}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"重做失败: {ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ExecuteNewWorkflow()
        {
            var tabControl = WorkflowTabViewModel;
            if (tabControl == null)
                return;

            // AddWorkflow 内部会触发 WorkflowAdded 事件，由 OnWorkflowAdded 统一处理
            // 引擎注册 + solution.Workflows 同步，避免双路径遗漏。
            tabControl.AddWorkflow();
            if (tabControl.SelectedTab != null)
            {
                LogSuccess($"创建新工作流: {tabControl.SelectedTab.Name}");
            }
        }

        private void ExecuteOpenWorkflow()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "工作流文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "打开工作流",
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var filePath = openFileDialog.FileName;
                    AddLog($"📂 正在加载工作流: {filePath}");

                    // 加载工作流
                    var success = _workflowEngine.LoadWorkflow(filePath);

                    if (!success)
                    {
                        AddLog("❌ 工作流加载失败，请查看日志了解详情");
                        return;
                    }

                    // 获取加载的工作流
                    var workflow = _workflowEngine.CurrentWorkflow;
                    if (workflow == null)
                    {
                        AddLog("❌ 加载的工作流为 null");
                        return;
                    }

                    AddLog($"✅ 工作流加载成功: {workflow.Name}");
                    AddLog($"   - 节点数量: {workflow.Nodes.Count}");
                    AddLog($"   - 连接数量: {workflow.Connections.Count}");

                    // 创建新的 UI 标签页
                    CreateWorkflowTab(workflow, filePath);

                    _currentWorkflowFilePath = filePath;
                    _isWorkflowModified = false;

                    AddLog($"✅ 工作流已在画布中打开");
                }
                catch (Exception ex)
                {
                    AddLog($"❌ 打开工作流时发生异常: {ex.Message}");
                    _logger?.LogError("打开工作流失败", "MainWindowViewModel", ex);
                }
            }
        }

        private void ExecuteSaveWorkflow()
        {
            if (WorkflowTabViewModel?.SelectedTab == null)
            {
                AddLog("❌ 没有选中的工作流标签页");
                return;
            }

            var selectedTab = WorkflowTabViewModel.SelectedTab;
            var workflowId = selectedTab.Id;

            // 如果没有文件路径，执行"另存为"
            if (string.IsNullOrEmpty(_currentWorkflowFilePath))
            {
                ExecuteSaveAsWorkflow();
                return;
            }

            try
            {
                AddLog($"💾 正在保存工作流到: {_currentWorkflowFilePath}");

                // 获取底层工作流对象
                var workflow = _workflowEngine.GetWorkflowById(workflowId);
                if (workflow == null)
                {
                    AddLog($"❌ 工作流不存在: {workflowId}");
                    return;
                }

                // 更新工作流节点（从 UI 层同步到底层）
                // 注意：节点已直接引用 Solution.Workflow.Nodes，无需手动同步

                // 保存工作流
                var success = _workflowEngine.SaveWorkflow(workflowId, _currentWorkflowFilePath);

                if (success)
                {
                    _isWorkflowModified = false;
                    AddLog($"✅ 工作流已成功保存: {_currentWorkflowFilePath}");
                }
                else
                {
                    AddLog("❌ 工作流保存失败，请查看日志了解详情");
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ 保存工作流时发生异常: {ex.Message}");
                _logger?.LogError("保存工作流失败", "MainWindowViewModel", ex);
            }
        }

        private void ExecuteSaveAsWorkflow()
        {
            if (WorkflowTabViewModel?.SelectedTab == null)
            {
                AddLog("❌ 没有选中的工作流标签页");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "工作流文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                DefaultExt = "json",
                Title = "保存工作流",
                FileName = WorkflowTabViewModel.SelectedTab.Name + ".json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var filePath = saveFileDialog.FileName;
                    var selectedTab = WorkflowTabViewModel.SelectedTab;
                    var workflowId = selectedTab.Id;

                    AddLog($"💾 正在保存工作流到: {filePath}");

                    // 获取底层工作流对象
                    var workflow = _workflowEngine.GetWorkflowById(workflowId);
                    if (workflow == null)
                    {
                        AddLog($"❌ 工作流不存在: {workflowId}");
                        return;
                    }

                    // 更新工作流（从 UI 层同步）
                    // 注意：节点已直接引用 Solution.Workflow.Nodes，无需手动同步

                    // 保存工作流
                    var success = _workflowEngine.SaveWorkflow(workflowId, filePath);

                    if (success)
                    {
                        _currentWorkflowFilePath = filePath;
                        _isWorkflowModified = false;
                        AddLog($"✅ 工作流已成功保存: {filePath}");
                    }
                    else
                    {
                        AddLog("❌ 工作流保存失败，请查看日志了解详情");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"❌ 另存工作流时发生异常: {ex.Message}");
                    _logger?.LogError("另存工作流失败", "MainWindowViewModel", ex);
                }
            }
        }

        /// <summary>
        /// 保存当前解决方案
        /// </summary>
        private void ExecuteSaveCurrentSolution()
        {
            try
            {
                var solutionManager = Adapters.ServiceInitializer.SolutionManager;

                // 1. 如果没有当前解决方案，弹出新建对话框
                SolutionMetadata? metadata = null;
                if (solutionManager.CurrentSolution == null)
                {
                    LogInfo("没有当前解决方案，弹出新建对话框");

                    // ✅ 统一使用解决方案管理器的默认目录
                    var defaultPath = solutionManager.SolutionsDirectory;
                    var dialog = new Views.Windows.NewSolutionDialog(defaultPath)
                    {
                        Owner = System.Windows.Application.Current.MainWindow
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        // 创建新解决方案元数据
                        metadata = new SolutionMetadata
                        {
                            Name = dialog.SolutionName,
                            Description = dialog.Description ?? "",
                            DirectoryPath = dialog.SolutionPath
                        };

                        // 构建文件路径
                        var filePath = string.IsNullOrEmpty(metadata.FilePath)
                            ? Path.Combine(metadata.DirectoryPath, $"{metadata.Name}.solution")
                            : metadata.FilePath;

                        // ✅ 直接创建 Solution 对象（不使用 CreateNewSolution）
                        // CreateNewSolution 是用于解决方案管理器的"新建解决方案"功能，会创建默认工作流
                        // 这里是"保存功能"，不应该创建默认工作流，而是同步 UI 中的数据
                        var solution = SunEyeVision.Workflow.Solution.Create();
                        solution.Version = metadata.Version;
                        solution.FilePath = filePath;

                        // ✅ 不添加默认工作流，保持 Workflows 为空

                        LogSuccess($"创建新解决方案对象: {metadata.Name}");

                        // ✅ 静默设置为当前解决方案（不触发事件）
                        // 注意：SetCurrentSolutionSilently 会设置 CurrentSolution 和 CurrentFilePath
                        solutionManager.SetCurrentSolutionSilently(solution, filePath);

                        // ✅ 更新必要的UI属性（不调用 UpdateCurrentSolutionInfo，避免触发事件）
                        CurrentSolutionName = metadata.Name;
                        CurrentSolutionPath = filePath;
                        OnPropertyChanged(nameof(HasCurrentSolution));

                        LogSuccess($"已设置当前解决方案: {metadata.Name}");
                    }
                    else
                    {
                        LogInfo("用户取消新建解决方案");
                        return;
                    }
                }

                // 2. 同步UI数据到Solution对象
                // SaveSolutionData 会遍历 UI 中的 Tabs，为每个 Tab 创建 workflow 并同步节点
                SaveSolutionData(solutionManager);

                // 3. 保存解决方案（此时 Solution 已包含 UI 中的工作流和节点）
                solutionManager.SaveSolution();

                // 4. 注册元数据（保存完成后注册）
                // 注意：SaveSolution 已经自动注册元数据，这里不需要再次注册
                // 因为 SaveSolution 内部会调用 _settings.KnownSolutions 和 _registry.Register
                var solutionName = metadata?.Name ?? CurrentSolutionName;
                LogSuccess($"已保存当前解决方案: {solutionName}");
            }
            catch (Exception ex)
            {
                LogError($"保存当前解决方案失败: {ex.Message}", null, ex);
            }
        }

        /// <summary>
        /// 更新当前解决方案信息
        /// </summary>
        private void UpdateCurrentSolutionInfo()
        {
            try
            {
                var solutionManager = Adapters.ServiceInitializer.SolutionManager;
                var currentSolution = solutionManager.CurrentSolution;

                if (currentSolution != null)
                {
                    var metadata = solutionManager.GetMetadata(currentSolution.Id);
                    CurrentSolutionName = metadata?.Name ?? "未命名解决方案";
                    CurrentSolutionPath = metadata?.FilePath ?? "";
                    LogInfo($"当前解决方案: {CurrentSolutionName}");
                }
                else
                {
                    CurrentSolutionName = string.Empty;
                    CurrentSolutionPath = string.Empty;
                }

                // 通知属性变化
                OnPropertyChanged(nameof(HasCurrentSolution));
            }
            catch (Exception ex)
            {
                LogError($"更新解决方案信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 当前解决方案变更事件处理
        /// </summary>
        private void OnCurrentSolutionChanged(object? sender, SolutionMetadataEventArgs e)
        {
            UpdateCurrentSolutionInfo();

            // 加载解决方案到工作流标签页
            var solutionManager = Adapters.ServiceInitializer.SolutionManager;
            var currentSolution = solutionManager?.CurrentSolution;

            if (currentSolution != null)
            {
                LoadSolutionToWorkflowTabs(currentSolution);
            }
            else
            {
                // ✅ 重置为默认状态（当删除当前解决方案时）
                // 与启动时保持一致，彻底清空节点和连接
                if (WorkflowTabViewModel != null)
                {
                    WorkflowTabViewModel.ResetToDefault();
                    LogInfo("已重置为默认状态（解决方案已关闭）");
                }
            }
        }

        /// <summary>
        /// 同步UI数据到Solution对象
        /// </summary>
        /// <param name="solutionManager">解决方案管理器</param>
        private void SaveSolutionData(SolutionManager solutionManager)
        {
            if (solutionManager.CurrentSolution == null)
            {
                LogWarning("无法保存数据：当前解决方案为空");
                return;
            }

            var solution = solutionManager.CurrentSolution;

            if (WorkflowTabViewModel == null)
                return;

            foreach (var tab in WorkflowTabViewModel.Tabs)
            {
                var workflow = solution.Workflows.FirstOrDefault(w => w.Id == tab.Id);

                if (workflow == null)
                {
                    // ✅ 优先复用引擎实例，引擎没有才创建新实例（避免序列化/运行时双实例漂移）
                    workflow = _workflowEngine.GetWorkflowById(tab.Id);
                    if (workflow == null)
                    {
                        workflow = new SunEyeVision.Workflow.Workflow(tab.Id, tab.Name);
                        _logger?.LogWarning(
                            $"[SaveSolutionData] 回退创建新 Workflow 实例（引擎中未找到）: {tab.Name} (ID: {tab.Id})");
                    }
                    solution.Workflows.Add(workflow);
                }
                else
                {
                    // 更新名称
                    if (workflow.Name != tab.Name)
                    {
                        workflow.Name = tab.Name;
                    }
                }

                // 同步节点：将引擎中该标签页根工作流（tab.Id）的节点同步到 Solution.Workflow.Nodes。
                // 不能拿 tab.WorkflowNodes，因为下钻子程序时它会被整体替换为子程序集合，
                // 导致主程序原有节点被下方 nodesToRemove 误清空。
                var rootWorkflow = _workflowEngine.GetWorkflow(tab.Id);
                if (rootWorkflow == null)
                    throw new InvalidOperationException($"SaveSolutionData: 引擎中未找到根工作流 (ID: {tab.Id})");

                foreach (var node in rootWorkflow.Nodes)
                {
                    if (!workflow.Nodes.Contains(node))
                    {
                        workflow.Nodes.Add(node);
                    }

                    // 关键日志：追踪区域数据序列化问题
                    if (node.Parameters != null)
                    {
                        // 尝试获取 InspectionRegions 信息
                        var paramsType = node.Parameters.GetType();
                        var inspectionRegionsProp = paramsType.GetProperty("InspectionRegions");
                        if (inspectionRegionsProp != null)
                        {
                            var inspectionRegions = inspectionRegionsProp.GetValue(node.Parameters);
                            if (inspectionRegions != null)
                            {
                                var regionsProp = inspectionRegions.GetType().GetProperty("Regions");
                                if (regionsProp != null)
                                {
                                    var regions = regionsProp.GetValue(inspectionRegions) as System.Collections.ICollection;
                                    var regionsCount = regions?.Count ?? 0;
                                    var regionsHashCode = regions?.GetHashCode() ?? 0;

                                    LogInfo($"[SaveSolutionData] 节点={node.Name} | ToolType={node.ToolType} | " +
                                        $"Parameters.HashCode={node.Parameters.GetHashCode():X8} | " +
                                        $"InspectionRegions.Regions.Count={regionsCount} | " +
                                        $"InspectionRegions.Regions.HashCode={regionsHashCode:X8}",
                                        "MainWindowViewModel");

                                    // 记录区域详细信息
                                    if (regionsCount > 0 && regions != null)
                                    {
                                        foreach (var region in regions)
                                        {
                                            var regionName = region?.GetType().GetProperty("Name")?.GetValue(region) ?? "Unknown";
                                            LogInfo($"[SaveSolutionData] 序列化区域 | Node={node.Name} | Region={regionName} | Type={region?.GetType().Name}",
                                                "MainWindowViewModel");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // 移除 Solution 中已从引擎根工作流删除的节点
                var nodesToRemove = workflow.Nodes
                    .Where(n => !rootWorkflow.Nodes.Contains(n))
                    .ToList();
                foreach (var node in nodesToRemove)
                {
                    workflow.Nodes.Remove(node);
                }

                // ✅ 优化：连接数据通过直接绑定 Solution.Workflow.Connections 实现，自动同步，无需手动操作
                // UI 层的 WorkflowTabViewModel.WorkflowConnections 直接引用 Solution 层的 workflow.Connections
                // 删除、添加连接会自动同步到 Solution，保存时无需额外同步代码
            }

            // ★ 确保子程序工作流也保存到 solution（它们没有独立 Tab，但属于引擎中的工作流）
            var subWorkflows = _workflowEngine.GetAllWorkflows()
                .Where(w => w.IsSubroutine && !solution.Workflows.Any(s => s.Id == w.Id))
                .ToList();
            foreach (var subWf in subWorkflows)
            {
                solution.Workflows.Add(subWf);
                LogInfo($"添加子程序工作流到 Solution: {subWf.Name} (ID: {subWf.Id})");
            }

            // ✅ 删除 Solution 中所有不在 UI 标签页中的工作流（解决创建新方案时多余工作流的问题）
            // 例如：创建新方案时默认创建的空"工作流1"会被删除，只保留UI中已编辑的工作流
            // 注意：需要处理同名但不同 ID 的情况（重命名而不是删除）
            // ★ 子程序工作流（IsSubroutine=true）不删除（它们没有独立 Tab）
            var workflowsToRemove = solution.Workflows
                .Where(w => !WorkflowTabViewModel.Tabs.Any(t => t.Id == w.Id) && !w.IsSubroutine)
                .ToList();

            foreach (var workflow in workflowsToRemove)
            {
                // 检查是否存在同名但不同 ID 的 UI 标签页
                var existingTab = WorkflowTabViewModel.Tabs.FirstOrDefault(t => t.Name == workflow.Name && t.Id != workflow.Id);

                if (existingTab != null)
                {
                    // ✅ 存在同名工作流，重命名而不是删除（保留数据）
                    LogWarning($"发现同名工作流: {workflow.Name} (SolutionId={workflow.Id}, UITabId={existingTab.Id})");
                    LogWarning($"重命名 Solution 工作流以避免冲突: {workflow.Name} → {workflow.Name}_old");
                    workflow.Name = $"{workflow.Name}_old";
                }
                else
                {
                    // ✅ 不存在同名工作流，可以安全删除
                    LogInfo($"删除 Solution 中多余的工作流: {workflow.Name} (Id={workflow.Id})");
                    solution.Workflows.Remove(workflow);
                }
            }
        }







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
        private void CreateWorkflowTab(SunEyeVision.Workflow.Workflow workflow, string? filePath)
        {
            LogInfo($"创建标签页: {workflow.Name}");

            // 创建 WorkflowTabViewModel
            var tabViewModel = new ViewModels.WorkflowTabViewModel();
            tabViewModel.Id = workflow.Id;
            tabViewModel.Name = workflow.Name;

            // 订阅重命名事件（更新引擎中的 Workflow.Name）
            tabViewModel.NameCommitted += OnTabNameCommitted;

            // Phase 2：通过 SetNodes 直接绑定外部集合中的相同节点实例
            // 节点在 WorkflowJsonConverter.Read 中已被 WorkflowNodeFactory 转换为 WorkflowNode
            // 因此 FromBase 为恒等转换（同一对象），不再创建副本
            try
            {
                tabViewModel.SetNodes(workflow.Nodes);
            }
            catch (Exception ex)
            {
                LogError($"节点绑定失败: {ex.Message}");
            }

            LogInfo($"节点加载完成，数量: {tabViewModel.WorkflowNodes.Count}/{workflow.Nodes.Count}");

            // ✅ 优化：直接绑定 Solution 层的连接集合，实现自动同步
            tabViewModel.SetConnections(workflow.Connections);
            LogInfo($"连接绑定完成，数量: {workflow.Connections.Count}");

            // 初始化空洞池（从现有节点恢复索引池）
            if (tabViewModel.SequenceManager != null)
            {
                tabViewModel.SequenceManager.InitializeHolePoolsFromNodes(tabViewModel.Id, tabViewModel.WorkflowNodes);
            }

            // 将工作流注册到 WorkflowEngine.Workflows 字典
            // 确保节点可以获取父节点数据
            _workflowEngine.RegisterWorkflow(workflow);
            LogInfo($"工作流已注册到 WorkflowEngine: {workflow.Name} (ID: {workflow.Id})");

            // 添加到标签页视图模型
            WorkflowTabViewModel?.Tabs.Add(tabViewModel);
            LogInfo($"标签页已添加: {workflow.Name}");
        }

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
        private void LoadSolutionToWorkflowTabs(Solution solution)
        {
            if (solution == null)
            {
                LogError("解决方案为空，无法加载");
                return;
            }

            if (WorkflowTabViewModel == null)
            {
                LogError("WorkflowTabViewModel 为空，无法加载解决方案");
                return;
            }

            try
            {
                LogInfo($"=== 开始加载解决方案 ===");
                LogInfo($"解决方案ID: {solution.Id}");
                LogInfo($"解决方案版本: {solution.Version}");
                LogInfo($"解决方案路径: {solution.FilePath ?? "（新解决方案）"}");
                LogInfo($"工作流数量: {solution.Workflows.Count}");
                LogInfo($"全局变量数量: {solution.GlobalVariables.Count}");
                LogInfo($"设备数量: {solution.Devices.Count}");
                LogInfo($"通讯配置数量: {solution.Communications.Count}");

                // 清空现有标签页
                var oldTabCount = WorkflowTabViewModel.Tabs.Count;

                // 重置所有工作流的节点序号管理器（防止全局索引污染）
                foreach (var tab in WorkflowTabViewModel.Tabs)
                {
                    if (tab.SequenceManager != null)
                    {
                        LogInfo($"重置工作流 {tab.Name} 的节点序号管理器");
                        tab.SequenceManager.Reset();
                    }
                }

                WorkflowTabViewModel.Tabs.Clear();
                LogInfo($"已清空 {oldTabCount} 个现有标签页");

                // 为每个工作流创建标签页
                int totalNodes = 0;
                int totalConnections = 0;
                int totalParams = 0;

                foreach (var workflow in solution.Workflows)
                {
                    LogInfo($"\n--- 加载工作流: {workflow.Name} (ID: {workflow.Id}) ---");
                    LogInfo($"节点数量: {workflow.Nodes.Count}");
                    LogInfo($"连接数量: {workflow.Connections.Count}");
                    LogInfo($"是否子程序: {workflow.IsSubroutine}");

                    if (workflow.IsSubroutine)
                    {
                        // 子程序工作流：只注册到引擎，不创建 Tab
                        _workflowEngine.RegisterWorkflow(workflow);
                        LogInfo($"注册子程序工作流到引擎: {workflow.Name} (ID: {workflow.Id})");
                    }
                    else
                    {
                        CreateWorkflowTab(workflow, solution.FilePath);
                    }

                    totalNodes += workflow.Nodes.Count;
                    totalConnections += workflow.Connections.Count;

                    // 统计参数数量
                    foreach (var node in workflow.Nodes)
                    {
                        if (node.Parameters != null)
                        {
                            totalParams += node.Parameters.GetAllParameterProperties().Count();
                        }
                    }
                }

                LogInfo($"\n=== 加载完成 ===");
                LogSuccess($"✓ 工作流数量: {solution.Workflows.Count}");
                LogSuccess($"✓ 节点总数: {totalNodes}");
                LogSuccess($"✓ 连接总数: {totalConnections}");
                LogSuccess($"✓ 参数总数: {totalParams}");
                LogSuccess($"✓ 节点参数已自动加载到 WorkflowNode.Parameters 中");

                // 验证加载结果
                if (WorkflowTabViewModel.Tabs.Count == 0)
                {
                    LogWarning("警告：加载后没有标签页，请检查解决方案文件");
                }
                else
                {
                    // 设置第一个标签页为选中标签页
                    WorkflowTabViewModel.SelectedTab = WorkflowTabViewModel.Tabs[0];
                    LogInfo($"已设置选中标签页: {WorkflowTabViewModel.SelectedTab.Name}");

                    // 初始化SmartPathConverter（用于连接线渲染）
                    Converters.Path.SmartPathConverter.Nodes = WorkflowTabViewModel.SelectedTab.WorkflowNodes;
                    Converters.Path.SmartPathConverter.Connections = WorkflowTabViewModel.SelectedTab.WorkflowConnections;
                    LogInfo($"SmartPathConverter已初始化，节点数: {WorkflowTabViewModel.SelectedTab.WorkflowNodes.Count}，连接数: {WorkflowTabViewModel.SelectedTab.WorkflowConnections.Count}");

                    LogSuccess($"标签页创建成功，当前标签页: {WorkflowTabViewModel.SelectedTab?.Name ?? "（无）"}");
                }
            }
            catch (Exception ex)
            {
                LogError($"加载解决方案到工作流标签页失败: {ex.Message}", null, ex);
                LogError($"堆栈跟踪: {ex.StackTrace}");
                throw;
            }
        }

        private async System.Threading.Tasks.Task ExecuteRunWorkflow()
        {
            AddLog("=== 开始执行工作流 ===");

            if (WorkflowTabViewModel == null)
            {
                AddLog("❌ WorkflowTabViewModel 为null");
                return;
            }

            if (WorkflowTabViewModel.SelectedTab == null)
            {
                AddLog("❌ 没有选中的工作流标签页");
                AddLog("💡 请确保一个标签页被选中");
                return;
            }

            AddLog($"📋 当前工作流: {WorkflowTabViewModel.SelectedTab.Name}");
            AddLog($"📊 节点数量: {WorkflowTabViewModel.SelectedTab.WorkflowNodes.Count}");
            AddLog($"🔗 连接数量: {WorkflowTabViewModel.SelectedTab.WorkflowConnections.Count}");

            if (WorkflowTabViewModel.SelectedTab.WorkflowNodes.Count == 0)
            {
                AddLog("⚠️ 当前工作流没有节点");
                AddLog("💡 请从工具箱拖拽算法节点到画布");
                AddLog("📝 可选节点：图像采集、灰度化、高斯模糊、阈值化、边缘检测、形态学");
                return;
            }

            IsRunning = true;
            AddLog("🚀 开始执行工作流...");

            try
            {
                // ★ 传递运行时参数到执行管理器
                _executionManager.SetRuntimeParameters(GetAllRuntimeParameters());

                // ★ 传递全局变量到执行管理器（用于 __GlobalVars__ 绑定解析）
                var solutionManager = Adapters.ServiceInitializer.SolutionManager;
                var globalVars = solutionManager.CurrentSolution?.GlobalVariables?
                    .ToDictionary(gv => gv.Name, gv => gv.Value);
                _executionManager.SetGlobalVariables(globalVars);

                await _executionManager.RunSingleAsync(WorkflowTabViewModel.SelectedTab);
                AddLog("✅ 执行完成");
            }
            catch (Exception ex)
            {
                AddLog($"❌ 执行失败: {ex.Message}");
                AddLog($"❌ 异常: {ex.StackTrace}");
            }
            finally
            {
                IsRunning = false;
            }
        }

        private void ExecuteStopWorkflow()
        {
            if (WorkflowTabViewModel.SelectedTab == null)
            {
                AddLog("⚠️ 没有选中的工作流标签页");
                return;
            }

            _executionManager.StopContinuousRun(WorkflowTabViewModel.SelectedTab);
            IsRunning = false;
            AddLog("⏹️ 工作流已停止");
        }

        private void ExecuteShowSettings()
        {
            // TODO: 显示设置对话框
        }

        private void ExecuteShowAbout()
        {
            var aboutWindow = new AboutWindow();
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
        public void SetRuntimeParameter<T>(string key, T value)
        {
            _runtimeParameters[key] = value!;
            AddLog($"📝 运行时参数已设置: {key} = {value}");
        }

        /// <summary>
        /// 获取运行时参数
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="key">参数键名</param>
        /// <returns>参数值</returns>
        public T? GetRuntimeParameter<T>(string key)
        {
            if (_runtimeParameters.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return default;
        }

        /// <summary>
        /// 获取所有运行时参数
        /// </summary>
        /// <returns>运行时参数字典</returns>
        public Dictionary<string, object> GetAllRuntimeParameters()
        {
            return new Dictionary<string, object>(_runtimeParameters);
        }

        /// <summary>
        /// 清除运行时参数
        /// </summary>
        public void ClearRuntimeParameters()
        {
            _runtimeParameters.Clear();
        }

        #endregion

        /// <summary>
        /// 添加节点到当前工作流
        /// </summary>
        public void AddNodeToWorkflow(WorkflowNodeBase node)
        {
            if (WorkflowTabViewModel.SelectedTab == null)
                return;

            // Phase 3: 确保节点是 WorkflowNode 实例（消除 FromBase 副本，实现对象级共享）
            if (node is not Models.WorkflowNode uiNode)
            {
                throw new InvalidOperationException(
                    $"节点必须是 WorkflowNode 实例。节点 {node.Id} 的实际类型为 {node.GetType().Name}");
            }

            AddLog($"➕ 添加节点: {node.Name} (ID={node.Id}, 类型={node.ToolType})");

            // ★ 子程序节点：自动创建子工作流并关联（用户无需手动配置）
            if (string.Equals(node.ToolType, "Subroutine", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(uiNode.SubroutineId))
            {
                CreateNewSubroutineWorkflow(uiNode);
                AddLog($"🆕 自动创建子程序工作流: {uiNode.DispName} (ID: {uiNode.SubroutineId})");
            }

            // 添加到 UI 集合
            var command = new AppCommands.AddNodeCommand(WorkflowTabViewModel.SelectedTab.WorkflowNodes, uiNode);
            WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);

            // 同步到引擎工作流（导航时 NavigateToWorkflowCanvas 从引擎读取，必须双向同步）
            var engineWf = _workflowEngine.GetWorkflow(WorkflowTabViewModel.SelectedTab.CurrentWorkflowId);
            if (engineWf != null && !engineWf.Nodes.Contains(uiNode))
            {
                engineWf.Nodes.Add(uiNode);
            }

            // ★ 添加节点后自动绑定输入图像源（子程序内部节点默认绑子程序输入图像；主流程无源则跳过）
            TryAutoBindImageSource(uiNode);
        }

        /// <summary>
        /// 添加连接到当前工作流（通过命令模式，支持连线覆盖）
        /// </summary>
        public void AddConnectionToWorkflow(WorkflowConnection connection)
        {
            if (WorkflowTabViewModel.SelectedTab == null)
                return;

            var connections = WorkflowTabViewModel.SelectedTab.WorkflowConnections;

            // 检查是否已存在相同端口对连接（两节点间有且仅有一条连线）
            var existingConnection = connections.FirstOrDefault(c =>
                c.SourceNodeId == connection.SourceNodeId &&
                c.TargetNodeId == connection.TargetNodeId &&
                c.SourcePort == connection.SourcePort &&
                c.TargetPort == connection.TargetPort);

            // 获取引擎工作流（同步用）
            var engineWf = _workflowEngine.GetWorkflow(WorkflowTabViewModel.SelectedTab.CurrentWorkflowId);

            if (existingConnection != null)
            {
                // 覆盖：用新连接替换旧连接
                var sourceNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
                var targetNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
                AddLog($"🔗 覆盖连接: {sourceNode?.Name ?? connection.SourceNodeId} → {targetNode?.Name ?? connection.TargetNodeId}");

                var command = new AppCommands.ReplaceConnectionCommand(connections, existingConnection, connection);
                WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);

                // 同步到引擎工作流
                if (engineWf != null)
                {
                    engineWf.Connections.Remove(existingConnection);
                    if (!engineWf.Connections.Contains(connection))
                        engineWf.Connections.Add(connection);
                }
            }
            else
            {
                // 新建连接
                var sourceNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
                var targetNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
                AddLog($"🔗 创建连接: {sourceNode?.Name ?? connection.SourceNodeId} → {targetNode?.Name ?? connection.TargetNodeId}");

                var command = new AppCommands.AddConnectionCommand(connections, connection);
                WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);

                // 同步到引擎工作流
                if (engineWf != null && !engineWf.Connections.Contains(connection))
                {
                    engineWf.Connections.Add(connection);
                }
            }

            // 连线建立后自动绑定 ImageSource（所见即所得）
            AutoBindImageSourceOnConnectionAdded(connection);

            // 连接变更后立即刷新当前选中节点的图像显示（无需用户切走再切回）
            RefreshImageDisplayIfAffected(connection);
        }

        /// <summary>
        /// 检查节点是否受指定连接的影响（作为连接的 Source 或 Target）
        /// </summary>
        private bool IsNodeAffectedByConnection(WorkflowNodeBase? node, WorkflowConnection connection)
        {
            if (node == null) return false;
            return node.Id == connection.SourceNodeId || node.Id == connection.TargetNodeId;
        }

        /// <summary>
        /// 如果当前选中节点受连接变更影响，强制刷新图像显示
        /// </summary>
        /// <remarks>
        /// 解决问题：连接节点后图像不立即更新，需切换节点再切回才显示最新结果
        /// </remarks>
        private void RefreshImageDisplayIfAffected(WorkflowConnection connection)
        {
            if (SelectedNode == null) return;

            if (IsNodeAffectedByConnection(SelectedNode, connection))
            {
                LogInfo($"连接变更|刷新图像显示: 受影响节点={SelectedNode.Name}({SelectedNode.Id}), 连接={connection.SourceNodeId}→{connection.TargetNodeId}", "ImageSourceSelector");

                // 使用 ForceSelectNode 强制刷新（即使节点引用相同也更新 DisplayImageSources + ImagePreviewVisibility）
                ForceSelectNode(SelectedNode);
            }
        }

        /// <summary>
        /// 连线建立后自动绑定 ImageSource — 确保所见即所得
        /// 同时对目标节点和源节点尝试自动绑定。
        /// 源节点绑定覆盖子工作流第一个节点（无上游连线）的场景。
        /// </summary>
        private void AutoBindImageSourceOnConnectionAdded(WorkflowConnection connection)
        {
            if (WorkflowTabViewModel.SelectedTab == null) return;

            var targetNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes
                .FirstOrDefault(n => n.Id == connection.TargetNodeId) as WorkflowNodeBase;
            if (targetNode != null)
                TryAutoBindImageSource(targetNode);

            if (connection.SourceNodeId != connection.TargetNodeId)
            {
                var sourceNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes
                    .FirstOrDefault(n => n.Id == connection.SourceNodeId) as WorkflowNodeBase;
                if (sourceNode != null)
                    TryAutoBindImageSource(sourceNode);
            }
        }

        /// <summary>
        /// 连线删除后自动重设 ImageSource — 确保所见即所得
        /// </summary>
        private void AutoBindImageSourceOnConnectionRemoved(
            string removedSourceNodeId,
            WorkflowNodeBase? targetNode)
        {
            if (targetNode?.Parameters == null) return;

            var imageSourceProp = targetNode.Parameters.GetType().GetProperty("ImageSource");
            if (imageSourceProp == null) return;

            var currentImageSource = imageSourceProp.GetValue(targetNode.Parameters) as AvailableDataSource;
            if (currentImageSource?.SourceNodeId != removedSourceNodeId) return;

            // 从 DSQS 获取剩余可用图像源，取第一个
            // 没有则清空（null），不兜底回退
            AvailableDataSource? firstImageSource = null;
            if (targetNode is UI.Models.WorkflowNode uiNode)
            {
                var dataProvider = GetDataProviderForNode(uiNode);
                if (dataProvider is IDataSourceQueryService queryService)
                {
                    firstImageSource = queryService.GetAvailableDataSources(targetNode.Id)
                        .FirstOrDefault(ds => ds.PropertyType != null
                            && typeof(IVisionImage).IsAssignableFrom(ds.PropertyType));
                }
            }

            imageSourceProp.SetValue(targetNode.Parameters, firstImageSource);

            if (firstImageSource != null)
            {
                AddLog($"图像源已更新: {targetNode.Name} → {firstImageSource.DropdownDisplayText}");
            }
            else
            {
                AddLog($"图像源已清空: {targetNode.Name}（无可用上游）");
            }
        }

        /// <summary>
        /// 从 DataSourceQueryService 取可用数据源列表中的第一个 IVisionImage 源，
        /// 统一设置到节点参数的 ImageSource 属性。
        /// 单一路径，无回退 — DataSourceQueryService 返回所有可用数据源
        /// （包含真实上游节点输出 + 子程序合成端口 __SubroutineInputs__）。
        /// </summary>
        private void TryAutoBindImageSource(WorkflowNodeBase node)
        {
            if (node?.Parameters == null) return;

            var imageSourceProp = node.Parameters.GetType().GetProperty("ImageSource");
            if (imageSourceProp == null) return;

            // 已有用户手动绑定（非自动默认源），不覆盖
            // ★ 默认源 __SubroutineInputs__（自动产生）允许被连线绑定覆盖（连线优先）
            if (imageSourceProp.GetValue(node.Parameters) is AvailableDataSource current
                && !string.IsNullOrEmpty(current.SourceNodeId)
                && current.SourceNodeId != "__SubroutineInputs__")
                return;

            if (node is not UI.Models.WorkflowNode uiNode) return;

            var dataProvider = GetDataProviderForNode(uiNode);
            if (dataProvider is not IDataSourceQueryService queryService) return;

            var firstImageSource = queryService.GetAvailableDataSources(node.Id)
                .FirstOrDefault(ds => ds.PropertyType != null
                    && typeof(IVisionImage).IsAssignableFrom(ds.PropertyType));

            if (firstImageSource != null)
            {
                imageSourceProp.SetValue(node.Parameters, firstImageSource);
                AddLog($"自动绑定图像源: {node.Name} → {firstImageSource.DropdownDisplayText}");
            }
        }

        /// <summary>
        /// 清除所有节点选中状态
        /// </summary>
        private void ClearNodeSelections()
        {
            if (WorkflowTabViewModel.SelectedTab == null)
                return;

            foreach (var node in WorkflowTabViewModel.SelectedTab.WorkflowNodes)
            {
                node.IsSelected = false;
            }
        }

        /// <summary>
        /// 清除所有连接线选中状态
        /// </summary>
        private void ClearConnectionSelections()
        {
            if (WorkflowTabViewModel.SelectedTab == null) return;
            foreach (var conn in WorkflowTabViewModel.SelectedTab.WorkflowConnections)
            {
                conn.IsSelected = false;
            }
        }

        /// <summary>
        /// 判断是否可以删除选中节点
        /// </summary>
        private bool CanDeleteSelectedNodes()
        {
            if (WorkflowTabViewModel.SelectedTab == null)
                return false;

            // 调试窗口打开时禁止删除节点（避免Delete键与区域编辑器冲突）
            if (IsDebugWindowOpen)
                return false;

            return WorkflowTabViewModel.SelectedTab.WorkflowNodes.Any(n => n.IsSelected)
                || WorkflowTabViewModel.SelectedTab.WorkflowConnections.Any(c => c.IsSelected);
        }

        /// <summary>
        /// 执行删除选中节点和连接线
        /// </summary>
        private void ExecuteDeleteSelectedNodes()
        {
            if (WorkflowTabViewModel.SelectedTab == null)
                return;

            var selectedNodes = WorkflowTabViewModel.SelectedTab.WorkflowNodes
                .Where(n => n.IsSelected).ToList();
            var selectedConnections = WorkflowTabViewModel.SelectedTab.WorkflowConnections
                .Where(c => c.IsSelected).ToList();

            if (selectedNodes.Count == 0 && selectedConnections.Count == 0)
                return;

            // 构建提示信息
            var parts = new List<string>();
            if (selectedNodes.Count > 0) parts.Add($"{selectedNodes.Count} 个节点");
            if (selectedConnections.Count > 0) parts.Add($"{selectedConnections.Count} 条连接线");

            var result = System.Windows.MessageBox.Show(
                $"确定要删除选中的 {string.Join("和 ", parts)} 吗？",
                "确认删除",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            // 执行前预计算所有将会被删除的连接（显式选中 + 与待删节点关联的，去重）
            var allAffectedConnectionIds = new HashSet<string>(
                selectedConnections.Select(c => c.Id));
            var allAffectedConnections = new List<WorkflowConnection>(selectedConnections);

            foreach (var node in selectedNodes)
            {
                foreach (var conn in WorkflowTabViewModel.SelectedTab.WorkflowConnections
                    .Where(c => c.SourceNodeId == node.Id || c.TargetNodeId == node.Id))
                {
                    if (allAffectedConnectionIds.Add(conn.Id))
                        allAffectedConnections.Add(conn);
                }
            }

            // 合并为一个原子命令，消除两步骤间的重叠删除问题
            var command = new AppCommands.BatchDeleteNodesAndConnectionsCommand(
                WorkflowTabViewModel.SelectedTab.WorkflowNodes,
                WorkflowTabViewModel.SelectedTab.WorkflowConnections,
                selectedNodes,
                selectedConnections);
            WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);

            // 同步删除到引擎工作流
            var engineWf = _workflowEngine.GetWorkflow(WorkflowTabViewModel.SelectedTab.CurrentWorkflowId);
            if (engineWf != null)
            {
                foreach (var conn in allAffectedConnections)
                {
                    engineWf.Connections.Remove(conn);
                }
                foreach (var node in selectedNodes)
                {
                    engineWf.Nodes.Remove(node);
                }
            }

            // 级联清理：清理被删节点关联的全局变量绑定
            foreach (var node in selectedNodes)
            {
                CleanupBindingsForDeletedNode(node.Id);
            }

            // ★ Phase 3：级联清理子程序工作流（删除 Subroutine 节点时同步清理对应后台工作流）
            foreach (var node in selectedNodes)
            {
                if (string.Equals(node.ToolType, "Subroutine", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(node.SubroutineId))
                {
                    var subId = node.SubroutineId;
                    _workflowEngine.DeleteWorkflow(subId);

                    var solMgr = Adapters.ServiceInitializer.SolutionManager;
                    if (solMgr?.CurrentSolution != null)
                    {
                        solMgr.CurrentSolution.RemoveWorkflow(subId);
                    }

                    AddLog($"🗑️ 级联清理子程序工作流: {node.Name} (ID: {subId})");
                }
            }

            // 处理被删连接的后置副作用（ImageSource 回退、日志）
            foreach (var conn in allAffectedConnections)
            {
                var targetNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes
                    .FirstOrDefault(n => n.Id == conn.TargetNodeId) as WorkflowNodeBase;
                AutoBindImageSourceOnConnectionRemoved(conn.SourceNodeId, targetNode);

                var sourceNodeName = WorkflowTabViewModel.SelectedTab.WorkflowNodes
                    .FirstOrDefault(n => n.Id == conn.SourceNodeId)?.Name ?? conn.SourceNodeId;
                var targetNodeName = targetNode?.Name ?? conn.TargetNodeId;
                AddLog($"⛓️ 删除连接: {sourceNodeName} → {targetNodeName}");
            }

            // 刷新当前选中节点的图像（如果受影响）
            if (allAffectedConnections.Count > 0)
                RefreshImageDisplayIfAffected(allAffectedConnections[^1]);

            // 清除选中状态
            SelectedNode = null;
            SelectedConnection = null;
            ClearNodeSelections();
            ClearConnectionSelections();

            AddLog($"🗑️ 已删除 {string.Join("和 ", parts)}");
        }

        private async void ExecuteOpenDebugWindow(Models.WorkflowNode? node)
        {
            if (node == null) return;

            try
            {
                var mainImageControl = GetMainImageControl?.Invoke();
                var context = new NodeOpenContext(Application.Current.MainWindow, mainImageControl);
                var decisionChain = new NodeOpenDecisionChain();
                var success = decisionChain.Execute(node.ToWorkflowNode(), context);

                if (!success)
                {
                    AddLog($"❌ 节点打开失败: {node.Name}");
                    return;
                }

                if (context.CreatedWindow == null)
                {
                    if (context.DebugControl is System.Windows.Controls.UserControl userControl)
                    {
                        // DecisionChain 将 FrameworkElement 留待调用方包装为 DefaultDebugWindow
                        var debugWindow = new SunEyeVision.Plugin.SDK.UI.Windows.DefaultDebugWindow(userControl);
                        if (context.MainWindow != null)
                            debugWindow.Owner = context.MainWindow;
                        context.CreatedWindow = debugWindow;
                    }
                    else
                    {
                        AddLog($"ℹ️ 节点 '{node.Name}' 无调试界面，无需打开窗口");
                        return;
                    }
                }

                // 调试窗口单例管理
                if (_openDebugWindow?.IsLoaded == true)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"调试窗口已打开（{_openDebugWindow.Title}）\n\n是否关闭当前窗口并打开新的调试窗口？",
                        "调试窗口已存在",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result != System.Windows.MessageBoxResult.Yes)
                    {
                        _openDebugWindow.Activate();
                        _openDebugWindow.Focus();
                        return;
                    }
                    _openDebugWindow.Close();
                }

                _openDebugWindow = context.CreatedWindow;
                _openDebugWindow.Title = node.Name;

                // ★ 动态工具（Script）：输出变更时实时刷新模块结果树（类似子程序的 onOutputsChanged 机制）
                Action? refreshContract = null;
                if (node.Parameters is INotifyOutputContractChanged contractNotifier)
                {
                    refreshContract = () =>
                    {
                        _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (node.Parameters is IOutputContractProvider provider)
                            {
                                var contract = provider.GetOutputContract();
                                if (contract.Count > 0)
                                    UpdateModuleResults(new ContractToolResults(contract), node.Name, node.ToolType, node.Id);
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    };
                    contractNotifier.OutputContractChanged += refreshContract;
                }

                _openDebugWindow.Closed += (s, e) =>
                {
                    // 清理输出变更订阅
                    if (refreshContract != null && node.Parameters is INotifyOutputContractChanged notifier)
                        notifier.OutputContractChanged -= refreshContract;

                    _openDebugWindow = null;
                    var imgCtrl = GetMainImageControl?.Invoke();
                    if (imgCtrl != null) imgCtrl.OverlayHitTestVisible = false;
                    if (SelectedNode?.IsSelected == true)
                    {
                        // 设计态：重新评估输出契约（脚本/子程序输出变更后刷新结果树）
                        if (SelectedNode.Parameters is IOutputContractProvider provider)
                        {
                            var contract = provider.GetOutputContract();
                            if (contract.Count > 0)
                            {
                                var tempResult = new ContractToolResults(contract);
                                UpdateModuleResults(tempResult, SelectedNode.Name, SelectedNode.ToolType, SelectedNode.Id);
                            }
                        }

                        var executionResult = CurrentWorkflowContext?.GetNodeResult(SelectedNode.Id);
                        if (executionResult?.Result != null)
                            _nodeResultManager.RefreshResultDisplay(SelectedNode, executionResult.Result);
                    }
                    AddLog($"🔧 关闭调试窗口: {node.Name}");
                };

                await InitializeToolControlAsync(_openDebugWindow, node);
                AddLog($"✅ 调试窗口已打开: {node.Name}");
            }
            catch (Exception ex)
            {
                AddLog($"打开节点失败: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"打开节点失败: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 控制流节点打开事件处理 - 由 NodeOpenDecisionChain 通过事件触发
        /// </summary>
        private void OnControlFlowNodeOpen(WorkflowNodeBase workflowNode)
        {
            var uiNode = WorkflowTabViewModel?.SelectedTab?.WorkflowNodes
                ?.FirstOrDefault(n => n.Id == workflowNode.Id);
            if (uiNode != null)
            {
                AddLog($"↪️ 双击子程序节点: {uiNode.Name}");
                OpenOrNavigateToSubroutine(uiNode);
            }
        }

        /// <summary>
        /// 打开或导航到子程序（VM风格：同一Tab内下钻，不新建Tab）
        /// 使用 SubroutineId 属性（而非 ExtraData）关联子程序工作流
        /// </summary>
        /// <param name="node">触发导航的子程序节点</param>
        private void OpenOrNavigateToSubroutine(Models.WorkflowNode node)
        {
            try
            {
                if (WorkflowTabViewModel?.SelectedTab == null)
                {
                    AddLog("❌ 没有选中的工作流标签页，无法导航到子程序");
                    return;
                }

                var selectedTab = WorkflowTabViewModel.SelectedTab;

                string subroutineId;

                // ★ 使用节点上的 SubroutineId 属性（SSOT，取代 ExtraData）
                subroutineId = node.SubroutineId ?? string.Empty;

                if (!string.IsNullOrEmpty(subroutineId))
                {
                    // 已有关联：同步节点 DispName 到引擎工作流名称（支持重命名）
                    var existingWorkflow = _workflowEngine.GetWorkflow(subroutineId);
                    if (existingWorkflow != null && existingWorkflow.Name != node.Name)
                    {
                        existingWorkflow.Name = node.Name;
                        AddLog($"🔄 同步子程序工作流名称: {existingWorkflow.Name}");
                    }
                    AddLog($"↪️ 导航到已有子程序: {node.Name} (ID: {subroutineId})");
                }
                else
                {
                    // 首次打开：创建子程序工作流并关联到节点
                    subroutineId = CreateNewSubroutineWorkflow(node);
                    AddLog($"🆕 创建并导航到子程序: {node.Name} (ID: {subroutineId})");
                }

                // 从引擎读取子程序工作流的实际名称
                var subWorkflow = _workflowEngine.GetWorkflow(subroutineId);
                var workflowDisplayName = node.Name;

                // 下钻导航
                selectedTab.NavigateToSubroutine(subroutineId, workflowDisplayName);

                // 加载子程序画布内容（从引擎获取子程序工作流的节点/连接）
                NavigateToWorkflowCanvas(subroutineId);

                AddLog($"✅ 子程序导航完成: {workflowDisplayName}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"导航到子程序失败: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                AddLog($"❌ 子程序导航失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 为子程序节点创建新的子程序工作流并关联（使用 SubroutineId 属性）
        /// </summary>
        private string CreateNewSubroutineWorkflow(Models.WorkflowNode node)
        {
            var subroutineId = Guid.NewGuid().ToString();

            // 注册到工作流引擎
            var workflow = _workflowEngine.CreateWorkflow(subroutineId, node.Name);
            workflow.IsSubroutine = true;

            // ★ 使用 SubroutineId 属性（不再是 ExtraData 弱类型字典）
            node.SubroutineId = subroutineId;

            // ★ 同步到 Solution（通过统一入口，和 OnWorkflowAdded 共用同一方法）
            SyncWorkflowToSolution(workflow, node.Name);

            // ★ 默认输入端口：新建子程序自动带 in_1（Image），供内部节点默认绑定（幂等，已有则不重复加）
            if (node.Parameters is SubroutineParameters subParams
                && !subParams.Inputs.Any(i => string.Equals(i.Name, "in_1", StringComparison.OrdinalIgnoreCase)))
            {
                subParams.Inputs.Add(new SubroutineInputConfig { Name = "in_1", BindingKey = "in_1", TypeName = "Image" });
                AddLog($"🆕 子程序默认输入端口: in_1 (Image)");
            }

            AddLog($"📝 新建子程序工作流: {node.Name} (ID: {subroutineId})");

            return subroutineId;
        }

        /// <summary>
        /// 导航到指定工作流ID对应的画布内容
        /// 从引擎读取工作流的节点/连接并绑定到 Tab
        /// </summary>
        private void NavigateToWorkflowCanvas(string workflowId)
        {
            var selectedTab = WorkflowTabViewModel?.SelectedTab;
            if (selectedTab == null) return;

            // ★ 清空当前画布上所有节点和连接的选中状态（防止切换后残留）
            foreach (var node in selectedTab.WorkflowNodes)
            {
                node.IsSelected = false;
            }
            foreach (var conn in selectedTab.WorkflowConnections)
            {
                conn.IsSelected = false;
            }

            var workflow = _workflowEngine.GetWorkflow(workflowId);
            if (workflow == null)
            {
                AddLog($"⚠️ 工作流未找到: {workflowId}，将使用空画布");
                return;
            }

            // 直接绑定引擎中的节点和连接集合到 Tab
            selectedTab.SetNodes(workflow.Nodes);
            selectedTab.SetConnections(workflow.Connections);

            AddLog($"📋 已加载画布: 工作流={workflow.Name}, 节点={workflow.Nodes.Count}, 连接={workflow.Connections.Count}");
        }

        /// <summary>
        /// 导航返回（由 [↩] 按钮触发）
        /// Tab 的 NavigateBack 已弹出工作流ID栈，此处加载对应画布内容
        /// </summary>
        public bool NavigateBackFromSubroutine()
        {
            if (WorkflowTabViewModel?.SelectedTab == null) return false;

            var tab = WorkflowTabViewModel.SelectedTab;
            if (!tab.NavigateBack()) return false;

            // 恢复上层工作流的画布内容
            NavigateToWorkflowCanvas(tab.CurrentWorkflowId);

            // 恢复上层工作流名称
            var parentWorkflow = _workflowEngine.GetWorkflow(tab.CurrentWorkflowId);
            tab.SetCurrentWorkflowName(parentWorkflow?.Name ?? tab.Name);

            AddLog($"↩️ 返回上层工作流: {tab.EffectiveDisplayName}");
            return true;
        }

        /// <summary>
        /// 更新图像显示控件的数据源（当前节点 + 所有上游节点）
        /// 统一使用 DataSourceQueryService.GetImageDisplaySources() 获取数据源，
        /// 与 ImageSourceSelector 共享相同的元数据源
        /// </summary>
        private void UpdateDisplayImageSources(Models.WorkflowNode? selectedNode)
        {
            // ★ 数据已在引擎中（SSOT），无需额外同步

            LogInfo($"图像源|UpdateDisplayImageSources 入口: node={(selectedNode != null ? $"{selectedNode.Name}({selectedNode.Id})" : "null")}, Clear前Count={DisplayImageSources.Count}", "ImageSourceSelector");
            DisplayImageSources.Clear();

            if (selectedNode == null)
            {
                LogInfo($"图像源|selectedNode==null, 清空数据源和显示", "ImageSourceSelector");
                // ★ Bug修复：当 selectedNode==null 时 UpdateDisplayImageFromCurrentSource
                //   会因为 _selectedNode==null 而直接返回（第2616行），导致 DisplayImage
                //   永远不会被清除。必须在 return 前直接清除，不能依赖延迟回调。
                DisplayImage = null;
                _nodeResultManager.ClearResultGraphics();
                SetSelectedDisplayImageSourceIndexDeferred(-1);
                return;
            }

            // 统一从 DataSourceQueryService 获取图像数据源
            // （走 IPropertyMetadataProvider，与 ImageSourceSelector 同源）
            var provider = GetDataProviderForNode(selectedNode);
            if (provider is DataSourceQueryService queryService)
            {
                var sources = queryService.GetImageDisplaySources(selectedNode.Id);
                foreach (var ds in sources)
                    DisplayImageSources.Add(ds);
                LogInfo($"图像源|数据源列表 (Count={DisplayImageSources.Count}): {string.Join(" | ", DisplayImageSources.Select((s, i) => $"[{i}]{s.SourceNodeName}.{s.DisplayName}"))}", "ImageSourceSelector");
            }
            else
            {
                LogWarning($"图像源|DataSourceQueryService 未找到, Provider type={provider?.GetType().Name ?? "null"}", "ImageSourceSelector");
            }

            // 从工作流引擎节点统一读取持久化的选中项（统一数据源）
            var workflow = _workflowEngine.CurrentWorkflow;
            var savedSource = workflow?.Nodes
                .FirstOrDefault(n => n.Id == selectedNode.Id)
                ?.SelectedDisplaySource;
            if (savedSource != null)
            {
                int idx = -1;
                for (int i = 0; i < DisplayImageSources.Count; i++)
                {
                    if (DisplayImageSources[i].SourceNodeId == savedSource.SourceNodeId &&
                        DisplayImageSources[i].PropertyName == savedSource.PropertyName)
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx >= 0)
                {
                    LogInfo($"图像源|命中持久化: ({savedSource.SourceNodeId}, {savedSource.PropertyName}), resolvedIdx={idx}", "ImageSourceSelector");
                    SetSelectedDisplayImageSourceIndexDeferred(idx);
                    return;
                }

                LogWarning($"图像源|持久化源未找到: ({savedSource.SourceNodeId}, {savedSource.PropertyName}), 回退到默认", "ImageSourceSelector");
            }
            else
            {
                LogInfo($"图像源|首次访问(无持久化)", "ImageSourceSelector");
            }

            // 默认选中当前节点的第一个输出
            int firstCurrent = -1;
            for (int i = 0; i < DisplayImageSources.Count; i++)
            {
                if (DisplayImageSources[i].SourceNodeId == selectedNode.Id)
                {
                    firstCurrent = i;
                    break;
                }
            }
            int targetIdx = firstCurrent >= 0 ? firstCurrent : (DisplayImageSources.Count > 0 ? 0 : -1);
            SetSelectedDisplayImageSourceIndexDeferred(targetIdx);

            // 节点已选中但无可显示图像源 → 清除残留图像和图形覆盖
            if (DisplayImageSources.Count == 0)
            {
                if (DisplayImage != null)
                {
                    LogInfo($"图像源|节点 {selectedNode.Name} 无输出图像, 清除残留显示", "ImageSourceSelector");
                    DisplayImage = null;
                }
                _nodeResultManager.ClearResultGraphics();
            }
        }

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
            var isSelectedSubroutine = (_selectedNode as Models.WorkflowNode)?.Id == nodeId;

            // 1. 结果树局部更新：仅当主界面选中该子程序节点时
            if (isSelectedSubroutine)
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
            if (_selectedNode is Models.WorkflowNode selected && selected.Id == nodeId)
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

        /// <summary>
        /// 延迟设置 SelectedDisplayImageSourceIndex，确保 WPF 先处理完 ItemsSource 的 CollectionChanged 事件
        /// （Clear + Add × N）后再设置 SelectedIndex，避免 ComboBox 内部因竞态条件选中项丢失
        /// 
        /// 使用两阶段策略：
        ///   阶段1: 先设 Property = -1（确保 WPF 绑定真正传播到 DP）
        ///   阶段2: 再设 Property = index（让 DP 回调可以执行重置 + 重选）
        /// 解决连续切换节点时 Property 值相同导致绑定不传播的问题
        /// </summary>
        private void SetSelectedDisplayImageSourceIndexDeferred(int index)
        {
            LogInfo($"图像源|Deferred排队: index={index}, 当前集合Count={DisplayImageSources.Count}", "ImageSourceSelector");
            Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    var currentProperty = SelectedDisplayImageSourceIndex;
                    var currentField = _selectedDisplayImageSourceIndex;
                    var comboMayAutoSelect = (currentField != -1 || currentProperty != -1);
                    LogInfo($"图像源|Deferred执行[阶段1]: 清除选中, 当前 Field={currentField}, Property={currentProperty}, " +
                            $"可能已被ComboBox自动选中={comboMayAutoSelect}", "ImageSourceSelector");

                    // 阶段1: 真正改变属性值为 -1，确保 WPF 绑定传播到 ImageControl 的 DP
                    // 此时 DP 回调检查 newIndex >= 0 → 不满足，仅记录日志
                    SelectedDisplayImageSourceIndex = -1;
                }));

            Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    _selectedDisplayImageSourceIndex = -1;
                    SelectedDisplayImageSourceIndex = index;
                    LogInfo($"图像源|Deferred执行[阶段2]: 设置选中 index={index}, " +
                            $"_selectedField={_selectedDisplayImageSourceIndex}", "ImageSourceSelector");
                }));
        }

        /// <summary>
        /// 根据当前 ComboBox 选中项更新显示图像
        /// </summary>
        private void UpdateDisplayImageFromCurrentSource()
        {
            if (_selectedNode == null) return;
            if (_selectedDisplayImageSourceIndex < 0 ||
                _selectedDisplayImageSourceIndex >= DisplayImageSources.Count)
            {
                // 正常的中间状态（Clear→Deferred 设置期间），最终会被修正
                return;
            }

            var selectedSource = DisplayImageSources[_selectedDisplayImageSourceIndex];
            LogInfo($"图像源|应用显示源: index={_selectedDisplayImageSourceIndex}, source={selectedSource.SourceNodeName}.{selectedSource.DisplayName} (SNID={selectedSource.SourceNodeId}, Prop={selectedSource.PropertyName})", "ImageSourceSelector");

            // 持久化：同步到节点的可序列化属性，保存工作流时自动携带
            var workflow = _workflowEngine.CurrentWorkflow;
            if (workflow != null)
            {
                var workflowNode = workflow.Nodes.FirstOrDefault(n => n.Id == _selectedNode.Id);
                if (workflowNode != null)
                {
                    workflowNode.SelectedDisplaySource = ImageSourceReference.FromDataSource(selectedSource);
                    LogInfo($"图像源|同步到可序列化属性: nodeId={_selectedNode.Id}, source={selectedSource.SourceNodeName}.{selectedSource.DisplayName}", "ImageSourceSelector");
                }
            }
            SetDisplayImageFromSource(selectedSource);
        }

        /// <summary>
        /// 根据选中的数据源从执行结果获取图像并显示
        /// </summary>
        private void SetDisplayImageFromSource(AvailableDataSource source)
        {
            var executionResult = CurrentWorkflowContext?.GetNodeResult(source.SourceNodeId);
            if (executionResult?.Result == null)
            {
                // 节点未执行过，无可显示图像 → 清除残留
                DisplayImage = null;
                return;
            }

            // 通过 NodeResultManager 获取对应的 Mat 并转换为 BitmapSource
            var bitmapSource = _nodeResultManager.GetDisplayImageFromSource(
                executionResult.Result, source.PropertyName);
            if (bitmapSource != null)
            {
                DisplayImage = bitmapSource;
            }
            else
            {
                DisplayImage = null;
            }
        }

        /// <summary>
        /// 获取当前 ComboBox 选中的数据源（供 NodeResultManager 使用）
        /// </summary>
        public AvailableDataSource? GetSelectedImageSource()
        {
            if (_selectedDisplayImageSourceIndex < 0 ||
                _selectedDisplayImageSourceIndex >= DisplayImageSources.Count)
                return null;
            return DisplayImageSources[_selectedDisplayImageSourceIndex];
        }

        /// <summary>
        /// 从 BitmapSource 设置显示图像（供 NodeResultManager 使用）
        /// </summary>
        public void SetDisplayImageFromBitmap(BitmapSource bitmapSource)
        {
            DisplayImage = bitmapSource;
        }

        /// <summary>
        /// 更新数据分析控件 - 根据选中节点是否提供数据分析控件来显示/隐藏
        /// </summary>
        /// <param name="selectedNode">当前选中的节点</param>
        private System.Threading.Tasks.Task UpdateDataAnalysisControl(Models.WorkflowNode? selectedNode)
        {
            try
            {
                // ★ 清理旧的选中高亮订阅（防止内存泄漏和残留高亮）
                if (SelectedNodeDataAnalysisControl is IToolControlDataAnalysis oldDaControl)
                {
                    oldDaControl.SelectionHighlightChanged -= OnSelectionHighlightChanged;
                }
                _nodeResultManager.ClearHighlight();

                // 清空当前控件
                SelectedNodeDataAnalysisControl = null;
                HasDataAnalysisControl = false;
                DataAnalysisControlTitle = "数据分析";

                if (selectedNode == null)
                    return System.Threading.Tasks.Task.CompletedTask;

                // 获取工具注册信息
                var toolType = selectedNode.ToolType;
                if (string.IsNullOrEmpty(toolType))
                    return System.Threading.Tasks.Task.CompletedTask;

                // 创建工具实例
                var tool = ToolRegistry.CreateToolInstance(toolType);
                if (tool == null)
                {
                    AddLog($"❌ 无法创建工具实例: {toolType}");
                    return System.Threading.Tasks.Task.CompletedTask;
                }

                // ★ 创建数据分析控件（独立于调试窗口）
                var dataAnalysisControl = tool.CreateDataAnalysisControl();
                if (dataAnalysisControl == null)
                {
                    // 工具未实现数据分析控件，隐藏Tab
                    HasDataAnalysisControl = false;
                    return System.Threading.Tasks.Task.CompletedTask;
                }

                // 设置控件属性
                DataAnalysisControlTitle = $"{selectedNode.Name} - 数据分析";
                SelectedNodeDataAnalysisControl = dataAnalysisControl;
                HasDataAnalysisControl = true;

                // ★ 订阅选中高亮联动事件（必须无条件订阅，否则数据执行后选中DataGrid不会渲染高亮）
                if (dataAnalysisControl is IToolControlDataAnalysis daControlForEvent)
                {
                    daControlForEvent.SelectionHighlightChanged += OnSelectionHighlightChanged;
                }

                // ★ 加载缓存结果（节点已执行过时，立即填充数据分析控件）
                var cachedResult = CurrentWorkflowContext?.GetNodeResult(selectedNode.Id);
                if (cachedResult?.Result != null && dataAnalysisControl is IToolControlDataAnalysis daControl)
                {
                    try
                    {
                        daControl.OnResultUpdated(cachedResult.Result);
                    }
                    catch (Exception loadEx)
                    {
                        AddLog($"⚠️ 加载缓存数据到分析控件失败: {loadEx.Message}");
                    }
                }

                AddLog($"✅ 已加载数据分析控件: {selectedNode.Name}");

                return System.Threading.Tasks.Task.CompletedTask;
            }
            catch (Exception ex)
            {
                AddLog($"❌ 更新数据分析控件失败: {ex.Message}");
                SelectedNodeDataAnalysisControl = null;
                HasDataAnalysisControl = false;
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }

        /// <summary>
        /// 数据分析控件选中高亮联动 → 将高亮元素设置到图像渲染层
        /// </summary>
        private void OnSelectionHighlightChanged(object? sender, System.Collections.Generic.IEnumerable<Plugin.SDK.Models.Visualization.DisplayItemBase>? highlightItems)
        {
            if (highlightItems == null)
                _nodeResultManager.ClearHighlight();
            else
                _nodeResultManager.SetHighlightItems(highlightItems);
        }



        /// <summary>
        /// 获取节点的数据提供者（按 WorkflowId 缓存，SSOT 委托读取，抛弃旧的 _nodeResults 本地缓存）
        /// </summary>
        /// <param name="node">节点</param>
        /// <returns>数据提供者实例</returns>
        private IDataSourceQueryService? GetDataProviderForNode(Models.WorkflowNode node)
        {
            try
            {
                // 获取当前工作流标签页
                var selectedTab = WorkflowTabViewModel?.SelectedTab;
                if (selectedTab == null)
                {
                    return null;
                }

                // 确定工作流 ID
                var workflowId = selectedTab.IsInSubroutine
                    ? selectedTab.CurrentWorkflowId
                    : _workflowEngine.CurrentWorkflow?.Id;

                if (workflowId == null)
                {
                    return null;
                }

                // 按 WorkflowId 缓存（GetOrAdd）；未命中 → 调用唯一工厂创建（工厂已内置子程序输入端口注入）
                if (!_workflowDsqsCache.TryGetValue(workflowId, out var queryService))
                {
                    queryService = CreateDataSourceQueryService(workflowId);
                    _workflowDsqsCache[workflowId] = queryService;
                }

                return queryService;
            }
            catch (Exception ex)
            {
                AddLog($"❌ 创建数据查询服务失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 为子程序工作流注册输入端口数据源提供者
        /// </summary>
        private void RegisterSubroutineInputProvider(DataSourceQueryService queryService, string subWorkflowId)
        {
            try
            {
                var parentNodeId = FindParentSubroutineNodeId(subWorkflowId);
                if (parentNodeId == null) return;

                WorkflowNodeBase? capturedParentNode = null;
                foreach (var wf in _workflowEngine.GetAllWorkflows())
                {
                    var found = wf.Nodes.FirstOrDefault(n => n.Id == parentNodeId);
                    if (found?.Parameters is SubroutineParameters)
                    {
                        capturedParentNode = found;
                        break;
                    }
                }

                if (capturedParentNode != null)
                {
                    queryService.AddExtraDataSourceProvider(() =>
                    {
                        if (capturedParentNode?.Parameters is not SubroutineParameters sp)
                            return new List<AvailableDataSource>();

                        var nodeName = capturedParentNode.Name ?? capturedParentNode.DispName ?? "子程序";
                        var sources = new List<AvailableDataSource>();
                        foreach (var input in sp.Inputs)
                        {
                            if (string.IsNullOrEmpty(input.Name)) continue;
                            var paramType = input.ParamType ?? typeof(object);
                            var bindingKey = input.BindingKey ?? input.Name;
                            sources.Add(new AvailableDataSource
                            {
                                SourceNodeId = "__SubroutineInputs__",
                                SourceNodeName = nodeName,
                                PropertyName = bindingKey,
                                DisplayName = input.Name,
                                PropertyType = paramType,
                                VariableType = SdkTypeVocabulary.ToTypeVocabulary(paramType),
                                Description = $"子程序输入: {input.Name}",
                            });
                        }
                        return sources;
                    });
                }
            }
            catch (Exception ex)
            {
                LogWarning($"⚠ 注册子程序输入端口提供者失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 为数据查询服务注册子程序输出数据源提供者。
        /// 遍历所有已注册工作流中的子程序节点，把用户声明的输出契约（含 Image 类型）暴露为额外数据源，
        /// 使主窗口图像下拉框（GetImageDisplaySources 步骤3）与下游绑定能发现子程序的动态输出。
        /// 输出额外数据源 SourceNodeId = 子程序节点 Id，选中后 GetDisplayImageFromSource 可定位到 SubroutineResults。
        /// </summary>
        private void RegisterSubroutineOutputProvider(DataSourceQueryService queryService)
        {
            try
            {
                var capturedWorkflows = _workflowEngine.GetAllWorkflows().ToList();

                queryService.AddExtraDataSourceProvider(() =>
                {
                    var sources = new List<AvailableDataSource>();
                    foreach (var wf in capturedWorkflows)
                    {
                        foreach (var node in wf.Nodes)
                        {
                            if (node.Parameters is not SubroutineParameters sp)
                                continue;

                            var nodeName = node.Name ?? node.DispName ?? "子程序";
                            foreach (var output in sp.Outputs)
                            {
                                if (string.IsNullOrEmpty(output.Name))
                                    continue;

                                var sysType = SdkTypeVocabulary.ToSystemType(output.TypeName) ?? typeof(object);
                                sources.Add(new AvailableDataSource
                                {
                                    SourceNodeId = node.Id,
                                    SourceNodeName = nodeName,
                                    PropertyName = output.Name,
                                    DisplayName = output.Name,
                                    PropertyType = sysType,
                                    VariableType = output.TypeName,
                                    GroupName = output.Group,
                                    Description = $"子程序输出: {output.Name}",
                                });
                            }
                        }
                    }
                    return sources;
                });
            }
            catch (Exception ex)
            {
                LogWarning($"⚠ 注册子程序输出提供者失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建 DataSourceQueryService（供子程序参数对话框使用，无需特定节点上下文）
        /// </summary>
        /// <param name="scopeWorkflowId">可选：限定到指定工作流的作用域（子程序内部数据源查询用）</param>
        public DataSourceQueryService CreateDataSourceQueryService(string? scopeWorkflowId = null)
        {
            var metadataProvider = new SunEyeVision.Plugin.Infrastructure.Managers.Tool.PropertyMetadataProviderAdapter();

            IWorkflowConnectionProvider connectionProvider;
            INodeInfoProvider nodeInfoProvider;

            if (scopeWorkflowId != null)
            {
                var subWorkflow = _workflowEngine.GetWorkflow(scopeWorkflowId)
                    ?? throw new InvalidOperationException($"子程序工作流未找到: {scopeWorkflowId}");
                connectionProvider = subWorkflow;
                nodeInfoProvider = subWorkflow;
                LogInfo($"✓ 创建作用域查询服务 → 子程序工作流: {subWorkflow.Name} (ID: {scopeWorkflowId})");
            }
            else
            {
                var workflow = _workflowEngine.CurrentWorkflow
                    ?? throw new InvalidOperationException("当前工作流为空");
                connectionProvider = workflow;
                nodeInfoProvider = workflow;
            }

            var queryService = new DataSourceQueryService(connectionProvider, nodeInfoProvider, null, metadataProvider);

            // SSOT 委托：实时读取 WorkflowContext，无本地缓存拷贝
            queryService.SetSootReader(nodeId =>
                CurrentWorkflowContext?.GetNodeResult(nodeId)?.Result);

            // ★ 作用域工作流是子程序时，注入其输入端口（__SubroutineInputs__），
            //    使子程序内部配置数据源时能看到上层子程序的输入（与 GetDataProviderForNode 行为对齐）
            //    作用域是主流程时 FindParentSubroutineNodeId 返回 null，自动跳过，零影响
            if (scopeWorkflowId != null)
            {
                RegisterSubroutineInputProvider(queryService, scopeWorkflowId);
            }

            // ★ 始终注册子程序输出提供者：主流程图像下拉框/下游绑定能发现子程序的动态输出
            //   （provider 每次查询动态调用，子程序输出配置变化实时生效）
            RegisterSubroutineOutputProvider(queryService);

            return queryService;
        }

        /// <summary>
        /// 在所有已注册工作流中查找引用指定子程序工作流 ID 的节点
        /// </summary>
        /// <param name="subroutineWorkflowId">子程序工作流 ID</param>
        /// <returns>父节点 ID，找不到则返回 null</returns>
        public string? FindParentSubroutineNodeId(string subroutineWorkflowId)
        {
            foreach (var workflow in _workflowEngine.GetAllWorkflows())
            {
                foreach (var node in workflow.Nodes)
                {
                    if (node.SubroutineId == subroutineWorkflowId)
                        return node.Id;
                }
            }
            return null;
        }

        /// <summary>
        /// 初始化调试目标（统一入口）
        /// </summary>
        /// <param name="target">初始化目标（IToolControlInitializer 或 Window）</param>
        /// <param name="node">当前节点</param>
        private async System.Threading.Tasks.Task InitializeToolControlAsync(object target, Models.WorkflowNode node)
        {
            // ★ 性能剪口：首次打开调试窗口的完整耗时
            var perfSw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 获取初始化接口
                var initializer = ResolveInitializer(target);
                if (initializer == null)
                {
                    AddLog($"⚠️ 初始化目标不支持 IToolControlInitializer: {target.GetType().Name}");
                    return;
                }

                // ① 设置当前节点
                var stepSw = System.Diagnostics.Stopwatch.StartNew();
                initializer.SetCurrentNode(node);
                stepSw.Stop();
                _logger?.LogInfo($"[Perf] SetCurrentNode: {stepSw.ElapsedMilliseconds}ms", "MainWindowViewModel");
                AddLog($"✅ 已设置当前节点");

                // ② 设置节点参数
                if (node.Parameters != null)
                {
                    stepSw.Restart();
                    initializer.SetParameters(node.Parameters);
                    stepSw.Stop();
                    _logger?.LogInfo($"[Perf] SetParameters: {stepSw.ElapsedMilliseconds}ms", "MainWindowViewModel");
                    AddLog($"✅ 已初始化节点参数");
                }

                // ③ 设置数据提供者
                var dataProvider = GetDataProviderForNode(node);
                if (dataProvider != null)
                {
                    stepSw.Restart();
                    initializer.SetDataProvider(dataProvider);
                    stepSw.Stop();
                    _logger?.LogInfo($"[Perf] SetDataProvider: {stepSw.ElapsedMilliseconds}ms", "MainWindowViewModel");
                    AddLog($"✅ 已初始化数据提供者");
                }

                // ④ 设置主窗口 ImageControl（可选）
                // 注意：已移到 debugWindow.Show() 之后调用，确保 OverlayCanvas 已准备就绪

                // ⑤ 异步初始化
                stepSw.Restart();
                await initializer.InitializeAsync();
                stepSw.Stop();
                _logger?.LogInfo($"[Perf] InitializeAsync: {stepSw.ElapsedMilliseconds}ms", "MainWindowViewModel");
                AddLog($"✅ 调试控件初始化完成");

                // 窗口路径的额外处理
                if (target is System.Windows.Window debugWindow)
                {
                    // 缓存数据提供者（已在 _workflowDsqsCache 中按 WorkflowId 缓存）

                    // 订阅执行完成事件
                    SubscribeToolExecutionCompleted(debugWindow, node, dataProvider!);

                    // 🔍 日志监控：准备调用 SetMainImageControl
                    AddLog($"🔍 [监控] 准备显示窗口并设置 ImageControl");

                    // 显示窗口（确保数据初始化完成后再显示，这样 Loaded 事件触发时所有数据已就绪）
                    stepSw.Restart();
                    debugWindow.Show();
                    stepSw.Stop();
                    _logger?.LogInfo($"[Perf] debugWindow.Show(): {stepSw.ElapsedMilliseconds}ms", "MainWindowViewModel");
                    AddLog($"✅ 调试窗口已显示");

                    // 缓存调试窗口（用于工作流执行完成后通知刷新）
                    _nodeDebugWindows[node.Id] = debugWindow;

                    // 🔍 日志监控：窗口已显示，准备获取 ImageControl
                    AddLog($"🔍 [监控] 窗口已调用 Show()，开始获取 ImageControl");

                    // ④ 设置主窗口 ImageControl（窗口显示后再设置，确保 OverlayCanvas 已准备就绪）
                    var mainImageControl = GetMainImageControl?.Invoke();
                    AddLog($"🔍 [监控] 获取到 ImageControl: {mainImageControl?.GetType().Name ?? "null"}");

                    // 🔍 日志监控：准备调用 SetMainImageControl
                    AddLog($"🔍 [监控] 准备调用 initializer.SetMainImageControl()");

                    initializer.SetMainImageControl(mainImageControl);

                    AddLog($"✅ 已设置主窗口 ImageControl");

                    // 窗口关闭清理
                    debugWindow.Closed += (s, e) =>
                    {
                        // 1. 移除调试窗口缓存（DSQS 仍在 _workflowDsqsCache 中缓存，工作流删除时统一清理）
                        _nodeDebugWindows.Remove(node.Id);

                        // 3. 恢复主窗口 ImageControl 的只读模式
                        var mainImageControl = GetMainImageControl?.Invoke();
                        if (mainImageControl != null)
                        {
                            mainImageControl.OverlayHitTestVisible = false;
                        }

                        // 4. 刷新当前选中节点的结果显示（如果有）
                        if (SelectedNode != null && SelectedNode.IsSelected)
                        {
                            var executionResult = CurrentWorkflowContext?.GetNodeResult(SelectedNode.Id);
                            if (executionResult?.Result != null)
                            {
                                _nodeResultManager.RefreshResultDisplay(SelectedNode, executionResult.Result);
                            }
                        }
                    };
                }

                perfSw.Stop();
                _logger?.LogInfo($"[Perf] 调试窗口打开总耗时: {perfSw.ElapsedMilliseconds}ms | 节点: {node.Name}", "MainWindowViewModel");
            }
            catch (Exception ex)
            {
                var errorDetails = new System.Text.StringBuilder();
                errorDetails.AppendLine($"❌ 初始化调试目标失败: {ex.Message}");
                var innerEx = ex.InnerException;
                while (innerEx != null)
                {
                    errorDetails.AppendLine($"  → 内部异常: {innerEx.Message}");
                    innerEx = innerEx.InnerException;
                }
                AddLog(errorDetails.ToString());
            }
        }

        /// <summary>
        /// 解析初始化接口
        /// </summary>
        private Plugin.SDK.UI.Controls.IToolControlInitializer? ResolveInitializer(object target)
        {
            // 直接实现接口
            if (target is Plugin.SDK.UI.Controls.IToolControlInitializer directInitializer)
                return directInitializer;

            // DefaultDebugWindow 实现了接口
            if (target is Plugin.SDK.UI.Windows.DefaultDebugWindow debugWindow)
                return debugWindow;

            // 兼容旧窗口：尝试从内部字段获取控件
            if (target is System.Windows.Window window)
            {
                var controlField = window.GetType().GetField("_control",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (controlField?.GetValue(window) is Plugin.SDK.UI.Controls.IToolControlInitializer innerInitializer)
                    return innerInitializer;
            }

            return null;
        }

        /// <summary>
        /// 订阅调试窗口的执行完成事件
        /// </summary>
        private void SubscribeToolExecutionCompleted(System.Windows.Window debugWindow, Models.WorkflowNode node,
            IDataSourceQueryService dataProvider)
        {
            var eventInfo = debugWindow.GetType().GetEvent("ToolExecutionCompleted");
            if (eventInfo == null)
            {
                AddLog($"⚠️ [调试窗口事件] {debugWindow.GetType().Name} 未定义 ToolExecutionCompleted 事件 | 结果将无法传播到数据分析控件");
                return;
            }

            AddLog($"🔍 [调试窗口事件] 找到 {eventInfo.EventHandlerType.Name} 事件，准备订阅 | 节点={node.Name}");

            // 创建事件处理器（使用 Delegate.CreateDelegate 动态创建）
            var eventHandlerType = eventInfo.EventHandlerType;

            // 创建委托方法
            var invokeMethod = eventHandlerType.GetMethod("Invoke");
            var parameters = invokeMethod?.GetParameters();

            if (parameters != null && parameters.Length == 2)
            {
                // 创建通用的处理方法
                Action<object?, object?> handlerAction = (sender, result) =>
                    {
                        try
                        {
                            // ★ 修复：从 EventArgs 中提取 ToolResults
                            var toolResult = result switch
                            {
                                SunEyeVision.Plugin.SDK.Execution.Results.ToolResults tr => tr,
                                SunEyeVision.Plugin.SDK.UI.Events.ToolExecutionCompletedEventArgs args => args.Results as SunEyeVision.Plugin.SDK.Execution.Results.ToolResults,
                                _ => null
                            };

                            if (toolResult != null)
                            {
                                // 执行结果已通过 NodeResultManager.UpdateNodeResult 存储到 WorkflowContext
                                // 这里不再需要直接赋值给 node.LastResult

                                // 更新图像显示（内部已存储到 WorkflowContext，DSQS 通过 _ssotReader 实时读取）
                                _nodeResultManager?.UpdateNodeResult(node, toolResult);

                                // ★ 通知调试窗口外部结果已到达（用于刷新位置修正矩阵等运行时状态）
                                NotifyDebugWindowExternalResult(node.Id);

                                // ★ 更新数据分析控件（调试窗口执行后立即刷新）
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (SelectedNodeDataAnalysisControl is Plugin.SDK.UI.Controls.IToolControlDataAnalysis daCtrl)
                                    {
                                        try
                                        {
                                            daCtrl.OnResultUpdated(toolResult);
                                        }
                                        catch (Exception onResultEx)
                                        {
                                            AddLog($"⚠️ 数据分析控件更新失败: {onResultEx.Message}");
                                        }
                                    }
                                });

                                AddLog($"✅ 调试窗口执行完成: {node.Name}");
                            }
                            else
                            {
                                AddLog($"⚠️ 调试窗口执行结果类型不匹配: {result?.GetType().Name ?? "null"}");
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog($"⚠️ 处理执行结果失败: {ex.Message}");
                        }
                    };

                // 创建委托
                var handler = Delegate.CreateDelegate(eventHandlerType, handlerAction.Target, handlerAction.Method);

                // 订阅事件
                eventInfo.AddEventHandler(debugWindow, handler);

                // 窗口关闭时取消订阅（防止内存泄漏）
                debugWindow.Closed += (s, e) =>
                {
                    eventInfo.RemoveEventHandler(debugWindow, handler);
                };
            }
        }

        /// <summary>
        /// 通知指定节点的调试窗口：外部结果已到达
        /// </summary>
        private void NotifyDebugWindowExternalResult(string nodeId)
        {
            if (_nodeDebugWindows.TryGetValue(nodeId, out var window))
            {
                try
                {
                    var method = window.GetType().GetMethod("OnExternalResultArrived");
                    if (method != null)
                    {
                        method.Invoke(window, null);
                        AddLog($"✅ 已通知调试窗口外部结果到达: {nodeId}");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"⚠️ 通知调试窗口外部结果失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 通知所有已打开的调试窗口：上游节点已执行完成，可刷新运行时状态
        /// </summary>
        private void NotifyAllDebugWindowsExternalResult()
        {
            AddLog($"🔍 [NotifyAll] 开始通知 | 缓存窗口数={_nodeDebugWindows.Count}");

            foreach (var kvp in _nodeDebugWindows.ToList())
            {
                try
                {
                    var window = kvp.Value;
                    var method = window.GetType().GetMethod("OnExternalResultArrived");

                    AddLog($"🔍 [NotifyAll] 节点={kvp.Key} | WindowType={window.GetType().Name} | MethodFound={method != null}");

                    if (method != null)
                    {
                        method.Invoke(window, null);
                        AddLog($"✅ [NotifyAll] 成功调用 OnExternalResultArrived: {kvp.Key}");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"⚠️ 通知调试窗口刷新失败 (节点 {kvp.Key}): {ex.Message}");
                }
            }
        }

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
            AddLog("🚀 开始执行所有工作流...");
            await WorkflowTabViewModel.RunAllWorkflowsAsync();
            AddLog("✅ 完成所有工作流");
        }

        /// <summary>
        /// 切换所有工作流运行/停止
        /// </summary>
        private void ExecuteToggleContinuousAll()
        {
            if (IsAllWorkflowsRunning)
            {
                AddLog("⏹️ 停止所有工作流");
                WorkflowTabViewModel.StopAllWorkflows();
            }
            else
            {
                AddLog("▶️ 开始所有工作流");
                WorkflowTabViewModel.StartAllWorkflows();
            }
        }

        /// <summary>
        /// 执行开始事件
        /// </summary>
        private void OnWorkflowExecutionStarted(object? sender, WorkflowExecutionEventArgs e)
        {
            AddLog($"▶️ 开始执行: {e.WorkflowId}");
        }

        /// <summary>
        /// 执行完成事件
        /// </summary>
        private void OnWorkflowExecutionCompleted(object? sender, WorkflowExecutionEventArgs e)
        {
            AddLog($"✅ 执行完成: {e.WorkflowId}");
        }

        /// <summary>
        /// 执行停止事件
        /// </summary>
        private void OnWorkflowExecutionStopped(object? sender, WorkflowExecutionEventArgs e)
        {
            AddLog($"⏹️ 执行已停止: {e.WorkflowId}");
        }

        /// <summary>
        /// 执行错误事件
        /// </summary>
        private void OnWorkflowExecutionError(object? sender, WorkflowExecutionEventArgs e)
        {
            AddLog($"❌ 执行错误: {e.WorkflowId} - {e.ErrorMessage}");
        }

        /// <summary>
        /// 执行进度事件
        /// </summary>
        private void OnWorkflowExecutionProgress(object? sender, WorkflowExecutionProgressEventArgs e)
        {
            try
            {
                AddLog(e.Message);
            }
            catch (Exception ex)
            {
AddLog($"⚠️ 日志记录异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 节点执行完成事件 - 更新节点结果并刷新UI
        /// </summary>
        private void OnNodeExecutionCompleted(object? sender, NodeExecutionResultEventArgs e)
        {
            try
            {
                // ★ 关键日志：节点执行完成 - 记录节点类型和顺序
                var outputValues = e.Result?.GetOutputValues();
                bool hasImageOutput = outputValues?.Any(kv => kv.Value is OpenCvSharp.Mat) ?? false;
                AddLog($"✅ 节点执行完成: {e.Node.Name}({e.Node.ToolType}), ID={e.Node.Id}, 成功={e.Result?.IsSuccess}, 有图像输出={hasImageOutput}");

                // ★ 诊断：检查 ShapeMatch 结果中包含的 BestPositionCorrection 属性
                if (outputValues != null)
                {
                    var hasPositionCorrection = outputValues.Any(kv =>
                        kv.Key.Contains("PositionCorrection", StringComparison.Ordinal) ||
                        kv.Key.Contains("BestPosition", StringComparison.Ordinal));
                    if (hasPositionCorrection)
                    {
                        AddLog($"📐 [位置修正] 节点 {e.Node.Name} 输出包含位置修正属性");
                    }
                }

                // 通过 NodeResultManager 更新节点结果（更新 OutputCache，不修改 InputSource）
                _nodeResultManager.UpdateNodeResult(e.Node, e.Result);

                // ★ 节点执行后处理（DSQS 自动读取 SSOT，无需推送）
                OnNodeExecutionPostProcess(e.Node, e.Result);

                // ★ 通知所有打开调试窗口的控件：上游节点执行完成，可刷新运行时状态
                NotifyAllDebugWindowsExternalResult();

                // ★ 更新数据分析控件（仅当完成节点是当前选中节点时）
                if (e.Node.Id == SelectedNode?.Id)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (SelectedNodeDataAnalysisControl is Plugin.SDK.UI.Controls.IToolControlDataAnalysis dataAnalysisControl
                            && e.Result != null && e.Result.IsSuccess)
                        {
                            try
                            {
                                dataAnalysisControl.OnResultUpdated(e.Result);
                            }
                            catch (Exception updateEx)
                            {
                                AddLog($"⚠️ 更新数据分析控件失败: {updateEx.Message}");
                            }
                        }
                    });
                }

                // 如果节点是当前显示节点，刷新输出缓存绑定
                if (_currentDisplayNodeId == e.Node.Id)
                {
                    var outputCache = e.Node.OutputCache;
                    if (outputCache != null && outputCache.HasOutput)
                    {
                        AddLog($"📷 节点 {e.Node.Name} 输出已更新, 输出图像数={outputCache.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
AddLog($"⚠️ 节点结果更新异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 节点执行完成后的后处理（DSQS 已通过 _ssotReader 委托自动读取 SSOT，无需推送）
        /// </summary>
        private void OnNodeExecutionPostProcess(Models.WorkflowNode node, Plugin.SDK.Execution.Results.ToolResults? result)
        {
            if (result == null || !result.IsSuccess) return;

            try
            {
                // ★ DSQS 已通过 _ssotReader 委托自动读取 WorkflowContext，无需手动推送
                //    删除旧的 UpdateNodeOutput / SetNodeResult 遍历代码

                var outputValues = result.GetOutputValues();
                if (outputValues == null || outputValues.Count == 0)
                {
                    AddLog($"⚠️ 节点 {node.Name} 无输出值");
                    return;
                }

                var allPropNames = string.Join(", ", outputValues.Keys);
                AddLog($"📤 节点 {node.Name}({node.ToolType}) 输出: [{allPropNames}] | ID={node.Id}");
            }
            catch (Exception ex)
            {
                AddLog($"⚠️ OnNodeExecutionPostProcess 异常: {ex.Message}");
            }
        }

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

        #region 图像视图

        /// <summary>
        /// 放大图像
        /// </summary>
        private void ExecuteZoomIn()
        {
            ImageScale = Math.Min(ImageScale * 1.2, 5.0);
            AddLog($"🔍 图像放大: {ImageScale:P0}");
        }

        /// <summary>
        /// 缩小图像
        /// </summary>
        private void ExecuteZoomOut()
        {
            ImageScale = Math.Max(ImageScale / 1.2, 0.1);
            AddLog($"🔍 图像缩小: {ImageScale:P0}");
        }

        /// <summary>
        /// 适应窗口
        /// </summary>
        private void ExecuteFitToWindow()
        {
            // TODO: 根据窗口大小计算缩放
            ImageScale = 1.0;
            AddLog($"🔍 适应窗口: {ImageScale:P0}");
        }

        /// <summary>
        /// 重置视图
        /// </summary>
        private void ExecuteResetView()
        {
            ImageScale = 1.0;
            AddLog($"🔄 重置视图: {ImageScale:P0}");
        }

        /// <summary>
        /// 切换全屏显示
        /// </summary>
        private void ExecuteToggleFullScreen()
        {
            // TODO: 实现图像全屏显示
            AddLog("🖥️ 切换全屏显示");
        }

        #endregion

        #region 图像操作

        /// <summary>
        /// 清除图像
        /// </summary>
        private void ExecuteClearImage()
        {
            try
            {
                DisplayImage = null;
                ImageScale = 1.0;
                AddLog("已清除图像");
            }
            catch (Exception ex)
            {
                AddLog($"清除图像失败: {ex.Message}");
            }
        }

        #endregion

        #region 图像预览

        /// <summary>
        /// 更新当前图像显示
        /// </summary>
        private void UpdateCurrentImageDisplay()
        {
            if (CurrentImageIndex < 0 || CurrentImageIndex >= ImageCollection.Count)
            {
                DisplayImage = null;
                return;
            }

            var imageInfo = ImageCollection[CurrentImageIndex];
            var fullImage = imageInfo.FullImage;

            if (fullImage != null)
            {
                DisplayImage = fullImage;
                AddLog($"📷 显示图像: {imageInfo.Name}");
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
        public void UpdateModuleResults(ToolResults? result, string? nodeName, string? toolId = null, string? nodeId = null,
            ToolParameters? parameters = null)
        {
            ModuleResultTree.Clear();
            HasModuleResults = false;

            // 统一以「输出变量契约 SSOT」为数据来源：
            //  - 运行期：直读 result.GetOutputVariables()（含 RuntimeValue），取代 ReflectGetValue + GetResultItems 兜底
            //  - 设计期（未运行）：回退到 ToolRegistry 缓存的契约元数据展示属性结构（占位符 --）
            var entries = new List<(string Name, string DisplayName, string? Group, Type? ClrType, object? RuntimeValue)>();

            if (result != null)
            {
                foreach (var v in result.GetOutputVariables())
                {
                    if (v.Kind != OutputVariableKind.Data)
                        continue;
                    entries.Add((
                        v.Name,
                        string.IsNullOrEmpty(v.DisplayName) ? v.Name : v.DisplayName,
                        v.VariableType == "Image" ? "图像" : v.Group, // 图像输出统一归入「图像」分组
                        SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToSystemType(v.VariableType),
                        v.RuntimeValue));
                }
            }
            else if (!string.IsNullOrEmpty(toolId))
            {
                foreach (var m in ToolRegistry.GetAllPropertyMetadata(toolId))
                {
                    // 设计态 RoI 契约变量：从参数回退读取检测区/屏蔽区结构（值缺失但结构应可见）
                    var designValue = ResolveRoiDesignValue(m.PropertyName, parameters);
                    entries.Add((
                        m.PropertyName,
                        m.DisplayName,
                        m.PropertyType != null && typeof(IVisionImage).IsAssignableFrom(m.PropertyType) ? "图像" : m.GroupName, // 图像归「图像」组
                        m.PropertyType,
                        designValue));
                }
            }
            else
            {
                return;
            }

            if (entries.Count == 0)
                return;

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[诊断:UpdateModuleResults] nodeName='{nodeName}' | toolId='{toolId}' | 契约变量数={entries.Count}",
                "MainWindowViewModel");

            // 根节点 = 当前选中节点名称
            var rootNode = new ModuleResultNode
            {
                DisplayName = nodeName ?? "结果",
                IsGroup = true,
                IsExpanded = true
            };

            // 按 Group 分组构建树（支持 / 分隔的层级分组）；图像显示摘要、RoI 字段聚合，均作为普通输出变量展示
            var groups = entries
                .GroupBy(e => e.Group ?? "其他")
                .OrderBy(g => g.Key);

            var groupNodeCache = new Dictionary<string, ModuleResultNode>();

            foreach (var group in groups)
            {
                if (string.IsNullOrEmpty(group.Key))
                    continue;

                var groupNames = group.Key.Split('/');
                ModuleResultNode currentParent = rootNode;

                for (int i = 0; i < groupNames.Length; i++)
                {
                    var cacheKey = string.Join("/", groupNames.Take(i + 1));

                    if (!groupNodeCache.TryGetValue(cacheKey, out var groupNode))
                    {
                        groupNode = new ModuleResultNode
                        {
                            DisplayName = groupNames[i],
                            IsGroup = true,
                            IsExpanded = true
                        };
                        currentParent.Children.Add(groupNode);
                        groupNodeCache[cacheKey] = groupNode;
                    }

                    currentParent = groupNode;
                }

                foreach (var e in group)
                {
                    var node = new ModuleResultNode
                    {
                        DisplayName = e.DisplayName,
                        DisplayValue = FormatTypedValue(e.RuntimeValue, e.ClrType),
                        PropertyName = e.Name,
                        SourceNodeId = nodeId,
                        SourcePropertyName = e.Name,
                        PropertyClrType = e.ClrType,
                        IsGroup = false
                    };

                    // 几何数组展开：按形状分组 + 字段聚合（方案：字段聚合防炸，不按 [i] 索引展开树）
                    // 例：检测区 5 项 → 圆(3项)/旋转矩形(2项) 分组，每字段一行，多值用分号串联
                    if (e.RuntimeValue is System.Collections.IEnumerable enumerable
                        && !(e.RuntimeValue is string)
                        && e.ClrType?.IsArray == true)
                    {
                        var elementType = e.ClrType.GetElementType();
                        if (elementType != null && typeof(IMonitorable).IsAssignableFrom(elementType))
                        {
                            BuildMonitorableArrayNodes(node, enumerable);
                        }
                    }
                    // 单值 IMonitorable 展开（如 IVisionImage → 宽度/高度/通道数）。
                    // 数组与 IMonitorable 实例互斥（数组变量已被 Promote 提升，不会走到此分支）。
                    else if (e.RuntimeValue is IMonitorable singleMonitorable)
                    {
                        BuildMonitorableFields(node, singleMonitorable);
                    }

                    currentParent.Children.Add(node);
                }
            }

            // 移除没有非可视化子节点的空组
            var emptyGroups = rootNode.Children
                .Where(c => c.IsGroup && c.Children.Count == 0)
                .ToList();
            foreach (var empty in emptyGroups)
                rootNode.Children.Remove(empty);

            if (rootNode.Children.Count > 0)
            {
                ModuleResultTree.Add(rootNode);
                HasModuleResults = true;
                BuildFlatRows();
            }
        }

        /// <summary>
        /// 按类型格式化反射取出的值
        /// </summary>
        private static string FormatTypedValue(object? value, Type propertyType)
        {
            // 长度 1 数组解包显示，避免单个 bool[1] 渲染成难看的 "[1个]"（底层仍是 T[1]，决策3）
            if (value is Array a && a.Length == 1)
                value = a.GetValue(0);

            return value switch
            {
                null => "--",
                double d  => $"{d:F3}",
                float f   => $"{f:F3}",
                int i     => i.ToString(),
                long l    => l.ToString(),
                bool b    => b ? "是" : "否",
                string s  => string.IsNullOrEmpty(s) ? "--" : s,
                // 数组类型：用分号连接显示实际值（而非 [N个]）
                double[] dArr => dArr.Length == 0
                    ? "--" : string.Join(";", dArr.Select(v => v.ToString("0.###"))),
                int[] iArr    => iArr.Length == 0
                    ? "--" : string.Join(";", iArr),
                string[] sArr => sArr.Length == 0
                    ? "--" : string.Join(";", sArr),
                // 图像类型：显示尺寸摘要，不落类型名（图像值走「图像结果」Tab，结果树仅示占位）
                IVisionImage image => $"[图像 {image.Width}×{image.Height}]",
                Array arr => $"[{arr.Length}个]",
                System.Collections.IEnumerable e when !(e is string) => $"[{e.Cast<object>().Count()}个]",
                _         => value.ToString() ?? "--"
            };
        }

        /// <summary>
        /// 构建可监视数组的字段聚合节点。
        /// 按形状分组（ShapeParameters 按 ShapeType），每字段一行，多元素值用分号串联，不按 [i] 索引展开树。
        /// </summary>
        private static void BuildMonitorableArrayNodes(ModuleResultNode parent, System.Collections.IEnumerable items)
        {
            var list = items.Cast<object>().Where(x => x is IMonitorable).ToList();
            if (list.Count == 0)
            {
                // 设计期（未运行/未绘制）：RoI(ShapeParameters[]) 空数组也按 ShapeType 枚举出结构，
                // 分组下用默认几何实例反射属性名生成属性行（值 --）。声明式，不依赖绘制数据（rule-016）。
                if (items is ShapeParameters[])
                {
                    BuildDesignTimeShapeGroups(parent);
                }
                return;
            }

            parent.IsGroup = true;
            parent.DisplayValue = null;

            // RoI 元素（ShapeParameters）：按形状分组后再字段聚合
            if (list[0] is ShapeParameters)
            {
                var shapeGroups = list
                    .Cast<ShapeParameters>()
                    .GroupBy(sp => sp.ShapeType)
                    .OrderBy(g => g.Key);

                foreach (var shapeGroup in shapeGroups)
                {
                    var shapeNode = new ModuleResultNode
                    {
                        DisplayName = $"{ShapeTypeDisplayName(shapeGroup.Key)} ({shapeGroup.Count()}项)",
                        IsGroup = true,
                        IsExpanded = false
                    };
                    AppendFieldAggregation(shapeNode, shapeGroup);
                    parent.Children.Add(shapeNode);
                }
                return;
            }

            // 普通几何数组（如算法产物 RotatedRect2d[]/Point2d[]）：直接字段聚合
            AppendFieldAggregation(parent, list);
        }

        /// <summary>
        /// 设计期 RoI 空数组结构：按 ShapeType 枚举出形状分组，分组下用默认几何实例反射属性字段名生成属性行（值 --）。
        /// 声明式，不读绘制数据（rule-016），创建节点即见完整三层结构；运行后由有值分支收敛为实际形状并填值。
        /// </summary>
        private static void BuildDesignTimeShapeGroups(ModuleResultNode parent)
        {
            parent.IsGroup = true;
            parent.DisplayValue = null;

            foreach (var type in Enum.GetValues<ShapeType>())
            {
                if (type == ShapeType.None) continue;

                var shapeNode = new ModuleResultNode
                {
                    DisplayName = ShapeTypeDisplayName(type),
                    IsGroup = true,
                    IsExpanded = false
                };
                AppendDesignTimeShapeFields(shapeNode, type);
                parent.Children.Add(shapeNode);
            }
        }

        /// <summary>
        /// 为设计期形状分组生成属性字段行（默认几何实例经 ShapeParameters.GetMonitorFields，值 --）。
        /// 与运行期同源：ShapeParameters.GetMonitorFields 对 Rectangle(Rect) 等有字段兜底（RoiDefinition）。
        /// </summary>
        private static void AppendDesignTimeShapeFields(ModuleResultNode shapeNode, ShapeType type)
        {
            var shape = new ShapeParameters { Geometry = RoiData.CreateDefaultGeometry(type) };
            if (shape.Geometry == null) return;

            var fields = shape.GetMonitorFields();
            foreach (var (fieldName, _) in fields)
            {
                shapeNode.Children.Add(new ModuleResultNode
                {
                    DisplayName = fieldName,
                    DisplayValue = "--",
                    IsGroup = false
                });
            }
        }

        /// <summary>
        /// 单值 IMonitorable 字段展开（如 IVisionImage → 宽度/高度/通道数）。
        /// 与数组版 BuildMonitorableArrayNodes 对称，与脚本监视树的 IMonitorable 展开对齐。
        /// 根节点保留原占位值（[图像 640×480]），元数据作为子节点展开。
        /// </summary>
        private static void BuildMonitorableFields(ModuleResultNode parent, IMonitorable monitorable)
        {
            var fields = monitorable.GetMonitorFields();
            if (fields.Count == 0)
                return;

            parent.IsGroup = true; // 有子节点 → 可展开
            foreach (var (fieldName, fieldValue) in fields)
            {
                parent.Children.Add(new ModuleResultNode
                {
                    DisplayName = fieldName,
                    DisplayValue = FormatTypedValue(fieldValue, fieldValue?.GetType()),
                    IsGroup = false
                });
            }
        }

        /// <summary>
        /// 把元素集合按字段聚合追加为子节点（每字段一行，值用分号串联）。
        /// 以第一个元素的字段列表为准，跨所有元素按字段名取值。
        /// </summary>
        private static void AppendFieldAggregation(ModuleResultNode parent, System.Collections.IEnumerable items)
        {
            var monitorableItems = items.Cast<object>().Where(x => x is IMonitorable).Cast<IMonitorable>().ToList();
            if (monitorableItems.Count == 0)
                return;

            var firstFields = monitorableItems[0].GetMonitorFields();
            foreach (var (fieldName, _) in firstFields)
            {
                var fieldValues = monitorableItems.Select(item =>
                {
                    var field = item.GetMonitorFields().FirstOrDefault(f => f.FieldName == fieldName);
                    return FormatTypedValue(field.FieldValue, field.FieldValue?.GetType() ?? typeof(object));
                });
                parent.Children.Add(new ModuleResultNode
                {
                    DisplayName = $"{fieldName} ({monitorableItems.Count}项)",
                    DisplayValue = string.Join("; ", fieldValues),
                    IsGroup = false
                });
            }
        }

        /// <summary>
        /// 设计态 RoI 契约变量回退：从工具参数读取检测区/屏蔽区结构。
        /// 结果运行值缺失时，仍应在结果树中展示用户已绘制的区域结构。
        /// </summary>
        private static object? ResolveRoiDesignValue(string propertyName, ToolParameters? parameters)
        {
            if (parameters is not SunEyeVision.Plugin.SDK.Execution.Parameters.RoiParameters regionParams)
                return null;

            IEnumerable<RoiData>? regions = propertyName switch
            {
                nameof(ToolResults.InspectionGeometries) => regionParams.InspectionRegions?.Regions,
                nameof(ToolResults.MaskGeometries) => regionParams.MaskRegions?.Regions,
                _ => null
            };

            return regions?
                .Where(r => r.IsEnabled)
                .Select(r => r.Parameters)
                .OfType<ShapeParameters>()
                .Where(sp => sp.Geometry != null)
                .ToArray();
        }

        /// <summary>
        /// ShapeType → 中文显示名（结果树形状分组节点用）
        /// </summary>
        private static string ShapeTypeDisplayName(SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeType shapeType)
        {
            return shapeType switch
            {
                SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeType.Circle => "圆",
                SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeType.Rectangle => "矩形",
                SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeType.RotatedRectangle => "旋转矩形",
                SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeType.Polygon => "多边形",
                SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeType.Annulus => "圆环",
                SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeType.Line => "直线",
                SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeType.Arc => "弧形",
                SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models.ShapeType.Point => "点",
                _ => shapeType.ToString()
            };
        }

        /// <summary>
        /// 判断是否为图像/图形类型（应在图像显示控件展示，不在结果表中展示）
        /// </summary>
        private static bool IsVisualResultType(Type? propertyType)
        {
            if (propertyType == null)
                return false;

            var fullName = propertyType.FullName;
            if (fullName == null)
                return false;

            // 图像类型：IVisionImage, HImage, HalImage, CvImage, Mat 及其派生
            if (fullName.Contains("IVisionImage") ||
                fullName.Contains("HImage") ||
                fullName.Contains("HalImage") ||
                fullName.Contains("CvImage") ||
                fullName.Contains("OpenCvSharp.Mat"))
                return true;

            // 区域/图形类型：IVisionRegion, CvRegion 及其派生
            if (fullName.Contains("IVisionRegion") ||
                fullName.Contains("CvRegion"))
                return true;

            // 检查基类链（处理继承关系）
            var baseType = propertyType.BaseType;
            while (baseType != null)
            {
                var baseName = baseType.FullName;
                if (baseName != null &&
                    (baseName.Contains("OpenCvSharp.Mat") ||
                     baseName.Contains("IVisionImage") ||
                     baseName.Contains("CvImage") ||
                     baseName.Contains("IVisionRegion") ||
                     baseName.Contains("CvRegion")))
                    return true;
                baseType = baseType.BaseType;
            }

            return false;
        }

        /// <summary>
        /// 将 ModuleResultTree 扁平化为一维列表供 DataGrid 绑定
        /// </summary>
        public void BuildFlatRows()
        {
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
        public void UpdateImagePreviewVisibility(Models.WorkflowNode? selectedNode)
        {
            // 1.没有选中节点时
            if (selectedNode == null)
            {
                // 只在当前状态不一致时才更新
                if (ShowImagePreview || ActiveInputSource != null || _currentDisplayNodeId != null)
                {
                    ShowImagePreview = false;
                    ActiveInputSource = null;
                    _currentDisplayNodeId = null;
                }
                return;
            }

            // 2.选中图像载入节点时，初始化显示图像预览（因为需要选择本地文件）
            if (selectedNode.IsImageLoadNode)
            {
                // 新架构：确保 InputSource 已初始化
                var inputSource = selectedNode.EnsureInputSource();

                // 检查是否是同一节点
                bool isSameNode = _currentDisplayNodeId == selectedNode.Id;

                if (!isSameNode)
                {
                    // 不同节点才更新
                    _currentDisplayNodeId = selectedNode.Id;
                    inputSource.PrepareForDisplay();
                    ActiveInputSource = inputSource;
                }

                // 只有真正变化时才设置
                if (!ShowImagePreview)
                {
                    ShowImagePreview = true;
                }
                return;
            }

            // 3.选中处理节点时，检查连接和上游图像载入节点
            // 核心原则：预览器必须绑定到图像载入节点的InputSource

            var connections = WorkflowTabViewModel?.SelectedTab?.WorkflowConnections;

            // 3.1 检查当前节点是否有输入连接
            bool hasInputConnection = connections?.Any(c => c.TargetNodeId == selectedNode.Id) ?? false;

            if (!hasInputConnection)
            {
                // ★ 子程序内部：不隐藏预览器，保持当前父工作流的图像预览
                if (WorkflowTabViewModel?.SelectedTab?.IsInSubroutine == true)
                    return;

                // 没有输入连接，隐藏预览器
                if (ShowImagePreview || ActiveInputSource != null || _currentDisplayNodeId != null)
                {
                    ShowImagePreview = false;
                    ActiveInputSource = null;
                    _currentDisplayNodeId = null;
                }
                return;
            }

            // 3.2 有输入连接，尝试逆向查找图像载入节点
            var sourceLoadNode = FindUpstreamImageLoadNode(selectedNode);

            if (sourceLoadNode != null)
            {
                // 找到上游图像载入节点，显示预览器（绑定到该节点）
                var sourceInputSource = sourceLoadNode.InputSource ?? sourceLoadNode.EnsureInputSource();

                bool isSameNode = _currentDisplayNodeId == sourceLoadNode.Id;

                if (!isSameNode)
                {
                    _currentDisplayNodeId = sourceLoadNode.Id;
                    sourceInputSource.PrepareForDisplay();
                    ActiveInputSource = sourceInputSource;
                }

                if (!ShowImagePreview)
                {
                    ShowImagePreview = true;
                }
            }
            else
            {
                // ★ 子程序内部：不隐藏预览器，保持当前父工作流的图像预览
                if (WorkflowTabViewModel?.SelectedTab?.IsInSubroutine == true)
                    return;

                // 找不到上游图像载入节点，隐藏预览器（没有数据源可绑定）
                if (ShowImagePreview || ActiveInputSource != null || _currentDisplayNodeId != null)
                {
                    ShowImagePreview = false;
                    ActiveInputSource = null;
                    _currentDisplayNodeId = null;
                }
            }
        }

        /// <summary>
        /// 强制刷新图像预览（从尺寸变化等触发）
        /// 即使当前 SelectedNode 未改变，也重新检查是否显示图像预览
        /// </summary>
        public void ForceRefreshImagePreview()
        {
            UpdateImagePreviewVisibility((Models.WorkflowNode?)_selectedNode);
        }

        /// <summary>
        /// 逆向追踪选中节点的图像载入节点（BFS）
        /// </summary>
        /// <remarks>
        /// 当存在多个载入节点时返回第一个找到的载入节点:
        /// 1. BFS保证路径最短
        /// 2. 节点ID排序保证选择一致
        /// </remarks>
        /// <param name="node">起始节点</param>
        /// <returns>第一个找到的图像载入节点，未找到返回null</returns>
        private Models.WorkflowNode? FindUpstreamImageLoadNode(Models.WorkflowNode node)
        {
            var selectedTab = WorkflowTabViewModel?.SelectedTab;
            if (selectedTab == null || selectedTab.WorkflowConnections == null || selectedTab.WorkflowNodes == null)
            {
                return null;
            }

            var visited = new HashSet<string>();
            var queue = new Queue<Models.WorkflowNode>();
            queue.Enqueue(node);
            visited.Add(node.Id);

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();

                // 获取上游节点ID，并在集合中排序后按节点ID验证正确性
                var upstreamNodeIds = selectedTab.WorkflowConnections
                    .Where(conn => conn.TargetNodeId == currentNode.Id)
                    .Select(conn => conn.SourceNodeId)
                    .Distinct()
                    .OrderBy(id => id) // 按节点ID排序，保证确定性
                    .ToList();

                foreach (var upstreamNodeId in upstreamNodeIds)
                {
                    if (visited.Contains(upstreamNodeId))
                    {
                        continue;
                    }

                    var upstreamNode = selectedTab.WorkflowNodes.FirstOrDefault(n => n.Id == upstreamNodeId);
                    if (upstreamNode == null)
                    {
                        continue;
                    }

                    // 找到图像载入节点，返回（第一个找到的）
                    if (upstreamNode.IsImageLoadNode)
                    {
                        return upstreamNode;
                    }

                    // 不是载入节点，继续追踪
                    visited.Add(upstreamNodeId);
                    queue.Enqueue(upstreamNode);
                }
            }

            return null;
        }


        #endregion

        #endregion // 显示

        #region 解决方案管理

        /// <summary>
        /// 显示解决方案配置界面
        /// </summary>
        private void ExecuteShowSolutionConfiguration()
        {
            try
            {
                LogInfo("打开解决方案配置界面");

                var solutionManager = Adapters.ServiceInitializer.SolutionManager;

                // [诊断] 记录打开前的状态
                var currentMetadata = solutionManager.CurrentSolution != null
                    ? solutionManager.GetMetadata(solutionManager.CurrentSolution.Id)
                    : null;
                LogInfo($"[诊断-打开] 当前解决方案ID: {solutionManager.CurrentSolution?.Id ?? "null"}");
                LogInfo($"[诊断-打开] 当前解决方案名称: {currentMetadata?.Name ?? "null"}");
                LogInfo($"[诊断-打开] 默认解决方案ID: {solutionManager.Settings.DefaultSolutionId ?? "null"}");
                LogInfo($"[诊断-打开] Settings.CurrentSolutionId: {solutionManager.Settings.CurrentSolutionId ?? "null"}");

                var configDialog = new Views.Windows.SolutionConfigurationDialog(solutionManager);

                var result = configDialog.ShowDialog();

                if (result == true && configDialog.IsLaunched && configDialog.LaunchResult != null)
                {
                    // 用户点击启动，加载解决方案
                    var solution = configDialog.LaunchResult;

                    try
                    {
                        // 设置当前解决方案（会触发 CurrentSolutionChanged 事件自动加载工作流）
                        solutionManager.SetCurrentSolution(solution);

                        var metadata = solutionManager.GetMetadata(solution.Id);
                        LogSuccess($"已加载解决方案: {metadata?.Name ?? "未命名"}");

                        // 更新标题
                        Title = $"太阳眼视觉 - {metadata?.Name ?? "未命名"}";

                        // [诊断] 记录加载后的状态
                        var loadedMetadata = solutionManager.CurrentSolution != null
                            ? solutionManager.GetMetadata(solutionManager.CurrentSolution.Id)
                            : null;
                        LogInfo($"[诊断-加载后] CurrentSolution: {loadedMetadata?.Name ?? "null"}");
                        LogInfo($"[诊断-加载后] CurrentSolutionId: {solutionManager.Settings.CurrentSolutionId ?? "null"}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"加载解决方案失败: {ex.Message}", null, ex);
                        MessageBox.Show(
                            $"加载解决方案失败: {ex.Message}",
                            "错误",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("打开解决方案配置界面失败", null, ex);
            }
        }

        /// <summary>
        /// 显示全局变量管理器
        /// </summary>
        private void ExecuteShowGlobalVariableManager()
        {
            try
            {
                LogInfo("打开全局变量管理器");

                var solutionManager = Adapters.ServiceInitializer.SolutionManager;
                var currentSolution = solutionManager.CurrentSolution;

                if (currentSolution == null)
                {
                    MessageBox.Show("请先打开一个解决方案", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var viewModel = new ViewModels.GlobalVariableManagerViewModel(solutionManager);
                var dialog = new Views.Windows.GlobalVariableManagerDialog(viewModel);

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                LogError($"打开全局变量管理器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 级联清理：删除节点时清理所有关联的 ConsumerBinding
        /// </summary>
        /// <param name="nodeId">被删除的节点ID</param>
        private void CleanupBindingsForDeletedNode(string nodeId)
        {
            var solution = Adapters.ServiceInitializer.SolutionManager?.CurrentSolution;
            if (solution?.GlobalVariables == null)
                return;

            foreach (var gv in solution.GlobalVariables)
            {
                // 清理 ConsumerBinding
                gv.ConsumerBindings.RemoveAll(c => c.NodeId == nodeId);
            }

            // 重建模块结果 FlatRows（变更新绑定状态显示）
            BuildFlatRows();
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

                var solutionManager = Adapters.ServiceInitializer.SolutionManager;
                LogInfo($"SolutionManager: {solutionManager != null}");

                var currentSolution = solutionManager?.CurrentSolution;
                LogInfo($"CurrentSolution: {currentSolution != null}");

                if (currentSolution == null)
                {
                    MessageBox.Show("请先打开一个解决方案", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                LogInfo("创建 CameraManagerViewModel...");
                var viewModel = new ViewModels.CameraManagerViewModel(solutionManager);
                LogInfo($"ViewModel 已创建, 相机数量: {viewModel.Cameras?.Count ?? 0}");

                LogInfo("创建 CameraManagerDialog...");
                var dialog = new Views.Windows.CameraManagerDialog(viewModel);

                LogInfo("显示对话框...");
                dialog.ShowDialog();

                LogInfo("对话框已关闭");
            }
            catch (Exception ex)
            {
                LogError($"打开相机管理器失败: {ex.Message}");
                LogError($"堆栈跟踪: {ex.StackTrace}");
                MessageBox.Show($"打开相机管理器失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 显示通讯管理器
        /// </summary>
        private void ExecuteShowCommunicationManager()
        {
            try
            {
                LogInfo("打开通讯管理器");

                var solutionManager = Adapters.ServiceInitializer.SolutionManager;
                var currentSolution = solutionManager.CurrentSolution;

                if (currentSolution == null)
                {
                    MessageBox.Show("请先打开一个解决方案", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var viewModel = new ViewModels.CommunicationManagerViewModel(solutionManager);
                var dialog = new Views.Windows.CommunicationManagerDialog(viewModel);

                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                LogError($"打开通讯管理器失败: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// 默认工具插件 - 用于测试
        /// </summary>
        private class DefaultToolPlugin : IToolPlugin
        {
            public Type ParamsType => typeof(GenericToolParameters);
            public Type ResultType => typeof(GenericToolResults);
            public IReadOnlyList<OutputVariable> OutputVariables => Array.Empty<OutputVariable>();

            public ToolResults Run(ToolParameters parameters)
            {
                return new GenericToolResults();
            }

            public bool HasDebugWindow => false;
            public System.Windows.Window? CreateDebugWindow() => null;
            public ToolParameters GetDefaultParameters() => new GenericToolParameters();
        }
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
    }

    /// <summary>
    /// 扁平化结果行（用于 DataGrid 显示树形结构）
    /// </summary>
    public class FlatResultRow : ObservableObject
    {
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
    }

    /// <summary>
    /// 输出契约包装器 — 动态工具设计态结果展示。
    /// 将 IOutputContractProvider 返回的 OutputVariable 列表包装为 ToolResults，
    /// 供 UpdateModuleResults 读取并建树。
    /// </summary>
    internal sealed class ContractToolResults : ToolResults
    {
        private readonly IReadOnlyList<OutputVariable> _contract;
        public ContractToolResults(IReadOnlyList<OutputVariable> contract) => _contract = contract;
        public override IReadOnlyList<OutputVariable> GetOutputVariables() => _contract;
    }
}
