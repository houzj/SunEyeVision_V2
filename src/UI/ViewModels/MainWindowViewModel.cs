using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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
using SunEyeVision.Plugin.SDK.Execution.Results;
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
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Logic;
using SunEyeVision.UI.Converters.Path;
using SunEyeVision.UI.Services.Performance;
using SunEyeVision.UI.Services.Logging;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Models;
using WorkflowNodeBase = SunEyeVision.Workflow.WorkflowNodeBase;

namespace SunEyeVision.UI.ViewModels
{
    /// <summary>
    /// 图像显示类型枚举
    /// </summary>
    public enum ImageDisplayType
    {
        Original,    // 原始图
        Processed,   // 处理图
        Result       // 结果图
    }

    /// <summary>
    /// 图像显示类型项
    /// </summary>
    public class ImageDisplayTypeItem
    {
        public ImageDisplayType Type { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

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

        // 图像类型
        private ImageDisplayTypeItem? _selectedImageType;
        private bool _showImagePreview = false;
        private BitmapSource? _originalImage;
        private BitmapSource? _processedImage;
        private BitmapSource? _resultImage;

        // 图像预览
        private bool _autoSwitchEnabled = false;
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
        private readonly ILogger _logger;

        // 执行管理
        private readonly WorkflowExecutionManager _executionManager;

        // 工作流上下文（用于存储执行结果）
        private readonly WorkflowContext _workflowContext;

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

        // 数据提供者缓存（用于在节点执行完成后更新前置节点输出）
        private readonly Dictionary<string, IDataSourceQueryService> _nodeDataProviders = new();

        private string _logText = "[系统] 等待中...\n";

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
            set => SetProperty(ref _selectedDisplayImageSourceIndex, value);
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
                bool changed = SetProperty(ref _selectedNode, value);

                if (changed)
                {
                    // 更新数据分析控件（同步等待异步操作）
                    UpdateDataAnalysisControl((Models.WorkflowNode?)value).GetAwaiter().GetResult();

                    // 刷新结果显示（如果节点有缓存结果）
                    var executionResult = value != null ? _workflowContext.GetNodeResult(value.Id) : null;
                    if (executionResult?.ToolResult != null)
                    {
                        _nodeResultManager.RefreshResultDisplay((Models.WorkflowNode?)value, executionResult.ToolResult);
                    }
                    else
                    {
                        // 清空结果显示
                        _nodeResultManager.ClearResultDisplay();
                    }

                    // 更新图像显示数据源（当前节点 + 父节点）
                    UpdateDisplayImageSources((Models.WorkflowNode?)value);

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
        public WorkflowViewModel WorkflowViewModel { get; set; }

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
        /// 图像显示类型集合
        /// </summary>
        public ObservableCollection<ImageDisplayTypeItem> ImageDisplayTypes { get; }

        /// <summary>
        /// 当前选中的图像显示类型
        /// </summary>
        public ImageDisplayTypeItem? SelectedImageType
        {
            get => _selectedImageType;
            set
            {
                if (SetProperty(ref _selectedImageType, value))
                {
                    UpdateDisplayImage();
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
        /// 原始图像
        /// </summary>
        /// <remarks>
        /// ★ 防闪烁优化：setter 不再自动更新 DisplayImage，由调用方显式控制
        /// </remarks>
        public BitmapSource? OriginalImage
        {
            get => _originalImage;
            set => SetProperty(ref _originalImage, value);
        }

        /// <summary>
        /// 处理后图像
        /// </summary>
        /// <remarks>
        /// ★ 防闪烁优化：setter 不再自动更新 DisplayImage，由调用方显式控制
        /// </remarks>
        public BitmapSource? ProcessedImage
        {
            get => _processedImage;
            set => SetProperty(ref _processedImage, value);
        }

        /// <summary>
        /// 结果图像
        /// </summary>
        /// <remarks>
        /// ★ 防闪烁优化：setter 不再自动更新 DisplayImage，由调用方显式控制
        /// </remarks>
        public BitmapSource? ResultImage
        {
            get => _resultImage;
            set => SetProperty(ref _resultImage, value);
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
        public ICommand BrowseImageCommand { get; }
        public ICommand LoadImageCommand { get; }
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
            // 删除全局 WorkflowNodes 和 WorkflowConnections 的初始化

            WorkflowViewModel = new WorkflowViewModel();
            WorkflowTabViewModel = new WorkflowTabControlViewModel();

            // 初始化图像显示类型
            ImageDisplayTypes = new ObservableCollection<ImageDisplayTypeItem>
            {
                new ImageDisplayTypeItem { Type = ImageDisplayType.Original, DisplayName = "原始图", Icon = "📷" },
                new ImageDisplayTypeItem { Type = ImageDisplayType.Processed, DisplayName = "处理图", Icon = "🔧" },
                new ImageDisplayTypeItem { Type = ImageDisplayType.Result, DisplayName = "结果图", Icon = "✅" }
            };
            SelectedImageType = ImageDisplayTypes.FirstOrDefault();

            // 初始化计算结果
            CalculationResults = new ObservableCollection<ResultItem>();

            // 初始化图像集合（优化版）
            ImageCollection = new BatchObservableCollection<ImageInfo>();

            // 初始化图像显示数据源
            DisplayImageSources = new ObservableCollection<AvailableDataSource>();

            // 初始化工作流引擎
            _logger = VisionLogger.Instance;
            _workflowEngine = new WorkflowEngine(_logger);

            // 初始化执行管理器
            _executionManager = new Services.Workflow.WorkflowExecutionManager(new Infrastructure.DefaultInputProvider());

            // 初始化工作流上下文
            _workflowContext = new WorkflowContext();

            // 初始化节点结果管理器
            _nodeResultManager = new Services.Workflow.NodeResultManager(_workflowContext, this);

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

            // 订阅工作流状态变化
            WorkflowTabViewModel.WorkflowStatusChanged += OnWorkflowStatusChanged;

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
            BrowseImageCommand = new RelayCommand(ExecuteBrowseImage);
            LoadImageCommand = new RelayCommand(ExecuteLoadImage);
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
            try
            {
                if (_workflowEngine != null && WorkflowTabViewModel?.SelectedTab != null)
                {
                    _workflowEngine.SetCurrentWorkflow(WorkflowTabViewModel.SelectedTab.Id);
                    _logger?.LogInfo($"OnSelectedTabChanged: Current workflow set to {WorkflowTabViewModel.SelectedTab.Name} (ID: {WorkflowTabViewModel.SelectedTab.Id})");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"OnSelectedTabChanged: Failed to set current workflow - {ex.Message}");
            }
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
            // TODO: 新建工作流
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
                    // 如果 Solution 中没有对应的 Workflow，创建一个
                    workflow = new SunEyeVision.Workflow.Workflow(tab.Id, tab.Name);
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

                // 同步节点：将 UI 集合中的节点同步到 Solution.Workflow.Nodes
                // 只添加 Solution 中不存在的节点（新增的节点通过 AddNodeToWorkflow 已同步）
                foreach (var node in tab.WorkflowNodes)
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

                // 移除 Solution 中已从 UI 删除的节点
                var nodesToRemove = workflow.Nodes
                    .Where(n => !tab.WorkflowNodes.Contains(n))
                    .ToList();
                foreach (var node in nodesToRemove)
                {
                    workflow.Nodes.Remove(node);
                }

                // ✅ 优化：连接数据通过直接绑定 Solution.Workflow.Connections 实现，自动同步，无需手动操作
                // UI 层的 WorkflowTabViewModel.WorkflowConnections 直接引用 Solution 层的 workflow.Connections
                // 删除、添加连接会自动同步到 Solution，保存时无需额外同步代码
            }

            // ✅ 删除 Solution 中所有不在 UI 标签页中的工作流（解决创建新方案时多余工作流的问题）
            // 例如：创建新方案时默认创建的空"工作流1"会被删除，只保留UI中已编辑的工作流
            // 注意：需要处理同名但不同 ID 的情况（重命名而不是删除）
            var workflowsToRemove = solution.Workflows
                .Where(w => !WorkflowTabViewModel.Tabs.Any(t => t.Id == w.Id))
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
        /// 根据算法类型确定节点类型
        /// </summary>
        private SunEyeVision.Core.Models.NodeType DetermineNodeTypeFromToolType(string toolType)
        {
            if (string.IsNullOrEmpty(toolType))
                return SunEyeVision.Core.Models.NodeType.Algorithm;

            // 图像采集类工具作为起始节点
            if (toolType.Contains("ImageCapture") ||
                toolType.Contains("ImageAcquisition") ||
                toolType.Contains("Camera") ||
                toolType.Contains("image_capture"))
            {
                return SunEyeVision.Core.Models.NodeType.Start;
            }

            // 图像载入类工具作为起始节点
            if (toolType.Contains("ImageLoad") ||
                toolType.Contains("image_load"))
            {
                return SunEyeVision.Core.Models.NodeType.Start;
            }

            // 控制类节点
            if (toolType.Contains("Subroutine") ||
                toolType.Contains("subroutine"))
            {
                return SunEyeVision.Core.Models.NodeType.Subroutine;
            }

            if (toolType.Contains("Condition") ||
                toolType.Contains("condition"))
            {
                return SunEyeVision.Core.Models.NodeType.Condition;
            }

            // 默认为算法节点
            return SunEyeVision.Core.Models.NodeType.Algorithm;
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

            // 使用统一转换方法将 WorkflowNodeBase → WorkflowNode
            // 确保位置、参数、尺寸等属性完整传递
            int nodeIndex = 0;
            foreach (var workflowNode in workflow.Nodes)
            {
                nodeIndex++;
                try
                {
                    tabViewModel.WorkflowNodes.Add(Models.WorkflowNode.FromBase(workflowNode));
                }
                catch (Exception ex)
                {
                    LogError($"节点加载失败 [{nodeIndex}/{workflow.Nodes.Count}]: {workflowNode.Name} - {ex.Message}");
                }
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

                // 重置工作流编号计数器（防止工作流序号污染）
                WorkflowTabViewModel.ResetWorkflowNumberCounter();
                LogInfo($"已重置工作流编号计数器");

                // 为每个工作流创建标签页
                int totalNodes = 0;
                int totalConnections = 0;
                int totalParams = 0;

                foreach (var workflow in solution.Workflows)
                {
                    LogInfo($"\n--- 加载工作流: {workflow.Name} (ID: {workflow.Id}) ---");
                    LogInfo($"节点数量: {workflow.Nodes.Count}");
                    LogInfo($"连接数量: {workflow.Connections.Count}");

                    CreateWorkflowTab(workflow, solution.FilePath);

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

            AddLog($"➕ 添加节点: {node.Name} (ID={node.Id}, 类型={node.ToolType})");

            // 添加到 UI 集合（通过命令模式，支持撤销）
            var uiNode = Models.WorkflowNode.FromBase(node);
            var command = new AppCommands.AddNodeCommand(WorkflowTabViewModel.SelectedTab.WorkflowNodes, uiNode);
            WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);

            // 同时添加到 Solution.Workflow.Nodes（同一对象引用）
            var solutionManager = Adapters.ServiceInitializer.SolutionManager;
            var workflow = solutionManager?.CurrentSolution?.Workflows.FirstOrDefault(w => w.Id == WorkflowTabViewModel.SelectedTab.Id);
            if (workflow != null && !workflow.Nodes.Contains(node))
            {
                workflow.Nodes.Add(node);
            }
        }

        /// <summary>
        /// 从当前工作流删除节点（通过命令模式）
        /// </summary>
        public void DeleteNodeFromWorkflow(WorkflowNodeBase node)
        {
            if (WorkflowTabViewModel.SelectedTab == null)
                return;

            AddLog($"➖ 删除节点: {node.Name} (ID={node.Id})");

            var command = new AppCommands.DeleteNodeCommand(
                WorkflowTabViewModel.SelectedTab.WorkflowNodes,
                WorkflowTabViewModel.SelectedTab.WorkflowConnections,
                Models.WorkflowNode.FromBase(node));
            WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);

            // 同时从 Solution.Workflow.Nodes 删除
            var solutionManager = Adapters.ServiceInitializer.SolutionManager;
            var workflow = solutionManager?.CurrentSolution?.Workflows.FirstOrDefault(w => w.Id == WorkflowTabViewModel.SelectedTab.Id);
            if (workflow != null)
            {
                workflow.Nodes.Remove(node);
            }
        }

        /// <summary>
        /// 移动节点到新位置（通过命令模式）
        /// </summary>
        public void MoveNode(WorkflowNodeBase node, Point newPosition)
        {
            var command = new AppCommands.MoveNodeCommand(Models.WorkflowNode.FromBase(node), node.Position, newPosition);
            if (WorkflowTabViewModel.SelectedTab != null)
            {
                WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);
            }
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

            if (existingConnection != null)
            {
                // 覆盖：用新连接替换旧连接
                var sourceNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
                var targetNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
                AddLog($"🔗 覆盖连接: {sourceNode?.Name ?? connection.SourceNodeId} → {targetNode?.Name ?? connection.TargetNodeId}");

                var command = new AppCommands.ReplaceConnectionCommand(connections, existingConnection, connection);
                WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);
            }
            else
            {
                // 新建连接
                var sourceNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
                var targetNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
                AddLog($"🔗 创建连接: {sourceNode?.Name ?? connection.SourceNodeId} → {targetNode?.Name ?? connection.TargetNodeId}");

                var command = new AppCommands.AddConnectionCommand(connections, connection);
                WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);
            }

            // 连线建立后自动绑定 ImageSource（所见即所得）
            AutoBindImageSourceOnConnectionAdded(connection);
        }

        /// <summary>
        /// 从当前工作流删除连接（通过命令模式）
        /// </summary>
        public void DeleteConnectionFromWorkflow(WorkflowConnection connection)
        {
            if (WorkflowTabViewModel.SelectedTab == null)
            {
                return;
            }

            // 获取节点名称用于日志
            var sourceNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
            var targetNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);
            AddLog($"⛓️ 删除连接: {sourceNode?.Name ?? connection.SourceNodeId} → {targetNode?.Name ?? connection.TargetNodeId}");

            // 删除前记录目标节点引用（删除后连接集合会变更）
            var targetNodeRef = targetNode as WorkflowNodeBase;

            var command = new AppCommands.DeleteConnectionCommand(WorkflowTabViewModel.SelectedTab.WorkflowConnections, connection);
            WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);

            // 连线删除后自动回退 ImageSource
            AutoBindImageSourceOnConnectionRemoved(connection.SourceNodeId, targetNodeRef);
        }

        /// <summary>
        /// 连线建立后自动绑定 ImageSource — 确保所见即所得
        /// </summary>
        /// <remarks>
        /// 核心逻辑：如果目标节点的 ImageSource 为 null（从未手动选过），
        /// 则自动设置为当前所有上游节点中第一个 Mat 类型的 AvailableDataSource。
        /// 这样不打开调试窗口时执行路径和打开调试窗口时显示路径始终一致。
        /// </remarks>
        private void AutoBindImageSourceOnConnectionAdded(WorkflowConnection connection)
        {
            if (WorkflowTabViewModel.SelectedTab == null) return;

            var targetNode = WorkflowTabViewModel.SelectedTab.WorkflowNodes
                .FirstOrDefault(n => n.Id == connection.TargetNodeId) as WorkflowNodeBase;
            if (targetNode?.Parameters == null) return;

            // 检查参数是否有 ImageSource 属性
            var imageSourceProp = targetNode.Parameters.GetType().GetProperty("ImageSource");
            if (imageSourceProp == null) return;

            // 如果 ImageSource 已有值（用户手动选过），不覆盖
            var currentImageSource = imageSourceProp.GetValue(targetNode.Parameters) as AvailableDataSource;
            if (currentImageSource != null) return;

            // 查找第一个 Mat 类型的上游数据源
            var firstMatSource = FindFirstMatDataSource(
                connection.TargetNodeId,
                WorkflowTabViewModel.SelectedTab.WorkflowConnections);

            if (firstMatSource != null)
            {
                imageSourceProp.SetValue(targetNode.Parameters, firstMatSource);
                AddLog($"自动绑定图像源: {targetNode.Name} → {firstMatSource.DropdownDisplayText}");
            }
        }

        /// <summary>
        /// 连线删除后自动回退 ImageSource — 确保所见即所得
        /// </summary>
        /// <remarks>
        /// 核心逻辑：如果当前 ImageSource 指向被删的源节点，
        /// 则自动回退到剩余上游的第一个 Mat 输出，无上游则清空。
        /// </remarks>
        private void AutoBindImageSourceOnConnectionRemoved(
            string removedSourceNodeId,
            WorkflowNodeBase? targetNode)
        {
            if (targetNode?.Parameters == null) return;

            // 检查参数是否有 ImageSource 属性
            var imageSourceProp = targetNode.Parameters.GetType().GetProperty("ImageSource");
            if (imageSourceProp == null) return;

            // 如果当前 ImageSource 不是指向被删的源节点，不需要处理
            var currentImageSource = imageSourceProp.GetValue(targetNode.Parameters) as AvailableDataSource;
            if (currentImageSource?.SourceNodeId != removedSourceNodeId) return;

            // 查找剩余上游的第一个 Mat 输出
            if (WorkflowTabViewModel.SelectedTab == null) return;

            var firstMatSource = FindFirstMatDataSource(
                targetNode.Id,
                WorkflowTabViewModel.SelectedTab.WorkflowConnections);

            // 有则回退，无则清空
            imageSourceProp.SetValue(targetNode.Parameters, firstMatSource);

            if (firstMatSource != null)
            {
                AddLog($"图像源自动回退: {targetNode.Name} → {firstMatSource.DropdownDisplayText}");
            }
            else
            {
                AddLog($"图像源已清空: {targetNode.Name}（无可用上游）");
            }
        }

        /// <summary>
        /// 从所有上游节点中查找第一个 Mat 类型的 AvailableDataSource
        /// </summary>
        /// <remarks>
        /// 与 ImageSourceSelector 的下拉列表过滤逻辑一致：
        /// 只返回 PropertyType.Name == "Mat" 的数据源。
        /// </remarks>
        private AvailableDataSource? FindFirstMatDataSource(
            string targetNodeId,
            ObservableCollection<WorkflowConnection> connections)
        {
            if (WorkflowTabViewModel.SelectedTab == null) return null;

            var metadataProvider = new PropertyMetadataProviderAdapter();
            var nodes = WorkflowTabViewModel.SelectedTab.WorkflowNodes;

            // 按连接顺序遍历所有上游节点
            var parentConnections = connections
                .Where(c => c.TargetNodeId == targetNodeId)
                .ToList();

            foreach (var conn in parentConnections)
            {
                var sourceNode = nodes.FirstOrDefault(n => n.Id == conn.SourceNodeId) as WorkflowNodeBase;
                if (sourceNode == null) continue;

                // 通过 PropertyMetadataProvider 获取源节点的所有输出属性元数据
                var metadataList = metadataProvider.GetAllPropertyMetadata(sourceNode.ToolType);
                if (metadataList == null) continue;

                // 找第一个 Mat 类型的属性（与 ImageSourceSelector 的过滤逻辑一致）
                var matMetadata = metadataList.FirstOrDefault(m =>
                    m.PropertyType?.Name == "Mat");

                if (matMetadata != null)
                {
                    return new AvailableDataSource
                    {
                        SourceNodeId = sourceNode.Id,
                        SourceNodeName = sourceNode.Name,
                        SourceNodeType = sourceNode.ToolType,
                        PropertyName = matMetadata.PropertyName,
                        DisplayName = matMetadata.DisplayName,
                        PropertyType = matMetadata.PropertyType,
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// 删除选中的节点（通过命令模式）
        /// </summary>
        public void DeleteSelectedNodes()
        {
            if (WorkflowTabViewModel.SelectedTab == null)
                return;

            var selectedNodes = WorkflowTabViewModel.SelectedTab.WorkflowNodes.Where(n => n.IsSelected).ToList();
            var command = new AppCommands.BatchDeleteNodesCommand(
                WorkflowTabViewModel.SelectedTab.WorkflowNodes,
                WorkflowTabViewModel.SelectedTab.WorkflowConnections,
                selectedNodes);
            WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);

            // 清除选中状态
            SelectedNode = null;
            ClearNodeSelections();
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

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                // 1. 先删除选中的连接线
                foreach (var conn in selectedConnections.ToList())
                {
                    DeleteConnectionFromWorkflow(conn);
                }

                // 2. 再删除选中的节点（BatchDeleteNodesCommand 会自动清理关联连接线）
                if (selectedNodes.Count > 0)
                {
                    var command = new AppCommands.BatchDeleteNodesCommand(
                        WorkflowTabViewModel.SelectedTab.WorkflowNodes,
                        WorkflowTabViewModel.SelectedTab.WorkflowConnections,
                        selectedNodes);
                    WorkflowTabViewModel.SelectedTab.CommandManager.Execute(command);
                }

                // 3. 清除选中状态
                SelectedNode = null;
                SelectedConnection = null;
                ClearNodeSelections();
                ClearConnectionSelections();

                AddLog($"🗑️ 已删除 {string.Join("和 ", parts)}");
            }
        }

        private async void ExecuteOpenDebugWindow(Models.WorkflowNode? node)
        {
            if (node != null)
            {
                try
                {
                    // 从 ToolRegistry 获取元数据
                    var toolId = node.ToolType ?? node.Name;
                    var toolMetadata = ToolRegistry.GetToolMetadata(toolId);

                    AddLog($"🔧 尝试打开调试窗口：工具ID={toolId}，节点名称={node.Name}");

                    if (toolMetadata == null)
                    {
                        AddLog($"❌ 未找到工具 '{toolId}' 的元数据信息");
                        System.Windows.MessageBox.Show(
                            $"未找到工具 '{toolId}' 的元数据信息",
                            "未找到工具",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    AddLog($"✅ 找到工具元数据：{toolMetadata.DisplayName}");

                    // 获取工具实例用于运行时检查
                    var tool = ToolRegistry.CreateToolInstance(toolId);
                    if (tool == null)
                    {
                        AddLog($"❌ 无法创建工具实例");
                        System.Windows.MessageBox.Show(
                            $"无法创建工具 '{toolId}' 的实例",
                            "无法创建工具",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    AddLog($"✅ 创建工具实例成功：{tool.GetType().Name}，HasDebugWindow={tool.HasDebugWindow}");

                    // 使用 NodeInterfaceFactory 获取界面类型（运行时检查版本）
                    var interfaceType = NodeInterfaceFactory.GetInterfaceType(node.ToWorkflowNode(), toolMetadata, tool);
                    AddLog($"🔍 界面类型：{interfaceType}");

                    switch (interfaceType)
                    {
                        case NodeInterfaceType.DebugWindow:
                            // 全局单例模式：有且只能有一个调试窗口被打开
                            if (_openDebugWindow != null && _openDebugWindow.IsLoaded)
                            {
                                // 已有窗口，询问用户是否切换
                                var result = System.Windows.MessageBox.Show(
                                    $"调试窗口已打开（{_openDebugWindow.Title}）\n\n是否关闭当前窗口并打开新的调试窗口？",
                                    "调试窗口已存在",
                                    System.Windows.MessageBoxButton.YesNo,
                                    System.Windows.MessageBoxImage.Question);

                                if (result != System.Windows.MessageBoxResult.Yes)
                                {
                                    // 用户选择不切换，激活现有窗口
                                    _openDebugWindow.Activate();
                                    _openDebugWindow.Focus();
                                    break;
                                }

                                // 关闭现有窗口
                                _openDebugWindow.Close();
                                _openDebugWindow = null;
                            }

                            // 使用新架构创建调试窗口
                            try
                            {
                                // 创建决策链
                                var decisionChain = new NodeOpenDecisionChain();

                                // 创建上下文
                                var mainImageControl = GetMainImageControl?.Invoke();
                                var context = new NodeOpenContext(
                                    Application.Current.MainWindow,
                                    mainImageControl)
                                {
                                    Node = node.ToWorkflowNode()
                                };

                                // 执行决策链
                                var success = decisionChain.Execute(context.Node, context);

                                if (success && context.CreatedWindow != null)
                                {
                                    // 获取创建的窗口
                                    var debugWindow = context.CreatedWindow;

                                    // 设置标题
                                    debugWindow.Title = node.Name;

                                    // 赋值到 _openDebugWindow，支持 IsDebugWindowOpen 检测
                                    _openDebugWindow = debugWindow;

                                    // 注册关闭事件，窗口关闭时清理引用
                                    debugWindow.Closed += (s, e) =>
                                    {
                                        // 1. 清除窗口引用
                                        _openDebugWindow = null;

                                        // 2. 恢复主窗口 ImageControl 的只读模式
                                        var mainImageControl = GetMainImageControl?.Invoke();
                                        if (mainImageControl != null)
                                        {
                                            mainImageControl.OverlayHitTestVisible = false;
                                        }

                                        // 3. 刷新当前选中节点的结果显示
                                        if (SelectedNode != null && SelectedNode.IsSelected)
                                        {
                                            var executionResult = _workflowContext.GetNodeResult(SelectedNode.Id);
                                            if (executionResult?.ToolResult != null)
                                            {
                                                _nodeResultManager.RefreshResultDisplay(SelectedNode, executionResult.ToolResult);
                                            }
                                        }

                                        AddLog($"🔧 关闭调试窗口: {node.Name}");
                                    };

                                    // 初始化调试目标（统一入口：注入参数、数据提供者、显示窗口）
                                    await InitializeToolControlAsync(debugWindow, node);
                                }
                                else
                                {
                                    AddLog($"⚠️ 工具 '{node.Name}' 无调试窗口或创建失败");
                                }
                            }
                            catch (Exception ex)
                            {
                                AddLog($"❌ 创建调试窗口失败: {ex.Message}");
                                System.Windows.MessageBox.Show(
                                    $"创建调试窗口失败: {ex.Message}",
                                    "错误",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Error);
                            }
                            break;

                        case NodeInterfaceType.NewWorkflowCanvas:
                            // 创建新的工作流标签页（子程序节点专用）
                            CreateSubroutineWorkflowTab(node);
                            break;

                        case NodeInterfaceType.SubroutineEditor:
                            // TODO: 实现节点编辑
                            System.Windows.MessageBox.Show(
                                "节点编辑功能待实现",
                                "提示",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information);
                            break;

                        case NodeInterfaceType.None:
                        default:
                            // 不显示任何界面
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"打开节点失败: {ex.Message}",
                        "错误",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    AddLog($"打开节点失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 为子程序节点创建新的标签页
        /// </summary>
        /// <param name="subroutineNode">子程序节点</param>
        private void CreateSubroutineWorkflowTab(Models.WorkflowNode subroutineNode)
        {
            try
            {
                if (WorkflowTabViewModel == null)
                {
                    AddLog("❌ WorkflowTabViewModel 为null");
                    return;
                }

                // 使用子程序节点名称作为工作流名称
                string workflowName = subroutineNode.Name;
                if (string.IsNullOrWhiteSpace(workflowName))
                {
                    workflowName = "子程序";
                }

                AddLog($"📝 创建子程序标签页: {workflowName}");

                // 创建新的工作流标签页
                var newWorkflowTab = new WorkflowTabViewModel
                {
                    Name = workflowName,
                    Id = Guid.NewGuid().ToString()
                };

                // ✅ 为没有绑定 Solution 的工作流创建独立的连接集合
                newWorkflowTab.SetConnections(new ObservableCollection<WorkflowConnection>());

                // 添加到标签页集合
                WorkflowTabViewModel.Tabs.Add(newWorkflowTab);

                // 选中新创建的标签页
                WorkflowTabViewModel.SelectedTab = newWorkflowTab;

                AddLog($"✅ 创建子程序 '{workflowName}' 成功");
                AddLog($"💡 现在可以在画布中添加子程序内部节点逻辑");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"创建子程序失败: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                AddLog($"❌ 创建子程序失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 使用 BFS 递归查找所有上游节点
        /// </summary>
        /// <param name="tab">当前工作流标签页</param>
        /// <param name="nodeId">起始节点ID</param>
        /// <returns>上游节点列表，按距离排序（最近的在前）</returns>
        private List<(Models.WorkflowNode Node, int Distance)> FindAllUpstreamNodes(
            ViewModels.WorkflowTabViewModel tab, string nodeId)
        {
            var result = new List<(Models.WorkflowNode Node, int Distance)>();
            var visited = new HashSet<string>();
            var queue = new Queue<(string NodeId, int Distance)>();

            // 从当前节点开始，查找所有前驱
            queue.Enqueue((nodeId, 0));
            visited.Add(nodeId);

            while (queue.Count > 0)
            {
                var (currentNodeId, distance) = queue.Dequeue();

                // 查找当前节点的所有直接前驱连接
                var parentConnections = tab.WorkflowConnections
                    .Where(conn => conn.TargetNodeId == currentNodeId)
                    .ToList();

                foreach (var connection in parentConnections)
                {
                    if (!visited.Contains(connection.SourceNodeId))
                    {
                        visited.Add(connection.SourceNodeId);

                        var parentNode = tab.WorkflowNodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
                        if (parentNode != null)
                        {
                            // 添加到结果（距离 +1）
                            result.Add((parentNode, distance + 1));

                            // 继续向上游查找
                            queue.Enqueue((connection.SourceNodeId, distance + 1));
                        }
                    }
                }
            }

            // 按距离排序（最近的在前）
            return result.OrderBy(x => x.Distance).ToList();
        }

        /// <summary>
        /// 更新图像显示控件的数据源（当前节点 + 所有父节点）
        /// </summary>
        /// <param name="selectedNode">当前选中的节点</param>
        private void UpdateDisplayImageSources(Models.WorkflowNode? selectedNode)
        {
            DisplayImageSources.Clear();

            if (selectedNode == null)
            {
                SelectedDisplayImageSourceIndex = -1;
                return;
            }

            // 获取当前工作流标签页
            var selectedTab = WorkflowTabViewModel?.SelectedTab;
            if (selectedTab == null)
            {
                SelectedDisplayImageSourceIndex = -1;
                return;
            }

            // 1. 首先添加当前节点（距离=0）
            if (selectedNode.OutputCache != null && selectedNode.OutputCache.HasOutput)
            {
                // 节点有输出，添加到列表
                var outputCache = selectedNode.OutputCache;
                foreach (var imageName in outputCache.GetImageNames())
                {
                    DisplayImageSources.Add(new AvailableDataSource
                    {
                        SourceNodeId = selectedNode.Id,
                        SourceNodeName = selectedNode.Name,
                        PropertyName = imageName,
                        DisplayName = string.IsNullOrEmpty(imageName) ? selectedNode.Name : imageName,
                        PropertyType = typeof(OpenCvSharp.Mat),
                        Distance = 0,
                        HasExecuted = true
                    });
                }
            }
            else
            {
                // 从 WorkflowContext 获取执行结果
                var executionResult = _workflowContext.GetNodeResult(selectedNode.Id);
                if (executionResult?.ToolResult != null && executionResult.Success)
                {
                    var outputValues = executionResult.ToolResult.GetOutputValues();
                    if (outputValues != null)
                    {
                        foreach (var kvp in outputValues.Where(kv => kv.Value is OpenCvSharp.Mat))
                        {
                            DisplayImageSources.Add(new AvailableDataSource
                            {
                                SourceNodeId = selectedNode.Id,
                                SourceNodeName = selectedNode.Name,
                                PropertyName = kvp.Key,
                                DisplayName = kvp.Key,
                                PropertyType = typeof(OpenCvSharp.Mat),
                                Distance = 0,
                                HasExecuted = true
                            });
                        }
                    }
                }
                else
                {
                    // 节点未执行，清空图像显示并添加占位项
                    DisplayImage = null;
                    OriginalImage = null;
                    ProcessedImage = null;
                    ResultImage = null;

                    DisplayImageSources.Add(new AvailableDataSource
                    {
                        SourceNodeId = selectedNode.Id,
                        SourceNodeName = $"{selectedNode.Name} (未执行)",
                        PropertyName = "Output",
                        DisplayName = "Output",
                        PropertyType = typeof(OpenCvSharp.Mat),
                        Distance = 0,
                        HasExecuted = false
                    });
                }
            }

            // 2. 使用 BFS 查找所有父节点
            var allUpstreamNodes = FindAllUpstreamNodes(selectedTab, selectedNode.Id);

            // 3. 添加所有父节点
            foreach (var (parentNode, distance) in allUpstreamNodes)
            {
                bool hasOutput = false;

                // 从 WorkflowContext 获取执行结果
                var executionResult = _workflowContext.GetNodeResult(parentNode.Id);
                if (executionResult?.ToolResult != null && executionResult.Success)
                {
                    var outputValues = executionResult.ToolResult.GetOutputValues();
                    if (outputValues != null)
                    {
                        foreach (var kvp in outputValues.Where(kv => kv.Value is OpenCvSharp.Mat))
                        {
                            DisplayImageSources.Add(new AvailableDataSource
                            {
                                SourceNodeId = parentNode.Id,
                                SourceNodeName = parentNode.Name,
                                PropertyName = kvp.Key,
                                DisplayName = kvp.Key,
                                PropertyType = typeof(OpenCvSharp.Mat),
                                Distance = distance,
                                HasExecuted = true
                            });
                            hasOutput = true;
                        }
                    }
                }

                // 如果没有输出，添加占位项
                if (!hasOutput)
                {
                    DisplayImageSources.Add(new AvailableDataSource
                    {
                        SourceNodeId = parentNode.Id,
                        SourceNodeName = $"{parentNode.Name} (未执行)",
                        PropertyName = "Output",
                        DisplayName = "Output",
                        PropertyType = typeof(OpenCvSharp.Mat),
                        Distance = distance,
                        HasExecuted = false
                    });
                }
            }

            // 4. 默认选中第一项（当前节点的第一个输出）
            if (DisplayImageSources.Count > 0)
            {
                SelectedDisplayImageSourceIndex = 0;
            }
            else
            {
                SelectedDisplayImageSourceIndex = -1;
            }
        }

        /// <summary>
        /// 更新数据分析控件 - 根据选中节点是否提供数据分析控件来显示/隐藏
        /// </summary>
        /// <param name="selectedNode">当前选中的节点</param>
        private System.Threading.Tasks.Task UpdateDataAnalysisControl(Models.WorkflowNode? selectedNode)
        {
            try
            {
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

                // ★ 加载缓存结果（节点已执行过时，立即填充数据分析控件）
                var cachedResult = _workflowContext.GetNodeResult(selectedNode.Id);
                if (cachedResult?.ToolResult != null && dataAnalysisControl is IToolControlDataAnalysis daControl)
                {
                    try
                    {
                        daControl.OnResultUpdated(cachedResult.ToolResult);
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
        /// 获取节点的数据提供者
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

                // 获取当前工作流
                var currentWorkflow = _workflowEngine.CurrentWorkflow;
                if (currentWorkflow == null)
                {
                    return null;
                }

                // 创建数据查询服务
                // 说明：WorkflowEngine 同时实现了两个接口：
                //   - IWorkflowConnectionProvider：提供工作流连接关系（父节点、子节点、连接线）
                //   - INodeInfoProvider：提供节点信息（结果类型、输出端口）
                // 由于架构分层限制，DataSourceQueryService 不能直接引用 WorkflowEngine 类，
                // 因此通过接口注入。虽然传入的是同一个对象 _workflowEngine，
                // 但它被分别用作两个不同的接口角色。

                // 创建属性元数据提供者
                var metadataProvider = new SunEyeVision.Plugin.Infrastructure.Managers.Tool.PropertyMetadataProviderAdapter();

                var queryService = new DataSourceQueryService(_workflowEngine, _workflowEngine, null, metadataProvider);
                LogInfo($"✓ 创建数据查询服务成功: 节点 {node.Id}, 工作流 {currentWorkflow.Name}");
                return queryService;
            }
            catch (Exception ex)
            {
                AddLog($"❌ 创建数据查询服务失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 初始化调试目标（统一入口）
        /// </summary>
        /// <param name="target">初始化目标（IToolControlInitializer 或 Window）</param>
        /// <param name="node">当前节点</param>
        private async System.Threading.Tasks.Task InitializeToolControlAsync(object target, Models.WorkflowNode node)
        {
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
                initializer.SetCurrentNode(node);
                AddLog($"✅ 已设置当前节点");

                // ② 设置节点参数
                if (node.Parameters != null)
                {
                    initializer.SetParameters(node.Parameters);
                    AddLog($"✅ 已初始化节点参数");
                }

                // ③ 设置数据提供者
                var dataProvider = GetDataProviderForNode(node);
                if (dataProvider != null)
                {
                    initializer.SetDataProvider(dataProvider);
                    AddLog($"✅ 已初始化数据提供者");
                }

                // ④ 设置主窗口 ImageControl（可选）
                // 注意：已移到 debugWindow.Show() 之后调用，确保 OverlayCanvas 已准备就绪

                // ⑤ 异步初始化
                await initializer.InitializeAsync();
                AddLog($"✅ 调试控件初始化完成");

                // 窗口路径的额外处理
                if (target is System.Windows.Window debugWindow)
                {
                    // 缓存数据提供者
                    _nodeDataProviders[node.Id] = dataProvider!;

                    // 订阅执行完成事件
                    SubscribeToolExecutionCompleted(debugWindow, node, dataProvider!);

                    // 🔍 日志监控：准备调用 SetMainImageControl
                    AddLog($"🔍 [监控] 准备显示窗口并设置 ImageControl");

                    // 显示窗口（确保数据初始化完成后再显示，这样 Loaded 事件触发时所有数据已就绪）
                    debugWindow.Show();

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
                        // 1. 移除数据提供者
                        _nodeDataProviders.Remove(node.Id);

                        // 2. 恢复主窗口 ImageControl 的只读模式
                        var mainImageControl = GetMainImageControl?.Invoke();
                        if (mainImageControl != null)
                        {
                            mainImageControl.OverlayHitTestVisible = false;
                        }

                        // 3. 刷新当前选中节点的结果显示（如果有）
                        if (SelectedNode != null && SelectedNode.IsSelected)
                        {
                            var executionResult = _workflowContext.GetNodeResult(SelectedNode.Id);
                            if (executionResult?.ToolResult != null)
                            {
                                _nodeResultManager.RefreshResultDisplay(SelectedNode, executionResult.ToolResult);
                            }
                        }
                    };
                }
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
            if (eventInfo != null)
            {
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
                                if (result is SunEyeVision.Plugin.SDK.Execution.Results.ToolResults toolResult)
                                {
                                    // 执行结果已通过 NodeResultManager.UpdateNodeResult 存储到 WorkflowContext
                                    // 这里不再需要直接赋值给 node.LastResult

                                    // 获取输出图像（通过反射调用 GetOutputValue 方法）
                                    var getOutputMethod = toolResult.GetType().GetMethod("GetOutputValue");
                                    var outputImage = getOutputMethod?.MakeGenericMethod(typeof(OpenCvSharp.Mat))
                                        .Invoke(toolResult, new object[] { "OutputImage" }) as OpenCvSharp.Mat;

                                    // 更新数据提供者中的节点输出
                                    if (outputImage != null)
                                    {
                                        dataProvider.UpdateNodeOutput(node.Id, "OutputImage", outputImage);
                                    }

                                    // 更新图像显示（内部已存储到 WorkflowContext）
                                    _nodeResultManager?.UpdateNodeResult(node, toolResult);

                                    // ★ 更新数据分析控件（调试窗口执行后立即刷新）
                                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (SelectedNodeDataAnalysisControl is Plugin.SDK.UI.Controls.IToolControlDataAnalysis daCtrl)
                                        {
                                            try { daCtrl.OnResultUpdated(toolResult); }
                                            catch { /* 忽略 */ }
                                        }
                                    });

                                    AddLog($"✅ 调试窗口执行完成: {node.Name}");
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
                // ★ 关键日志：节点执行完成
                var outputValues = e.Result?.GetOutputValues();
                bool hasImageOutput = outputValues?.Any(kv => kv.Value is OpenCvSharp.Mat) ?? false;
                AddLog($"✅ 节点执行完成: {e.Node.Name}, 成功={e.Result?.IsSuccess}, 有图像输出={hasImageOutput}");

                // 通过 NodeResultManager 更新节点结果（更新 OutputCache，不修改 InputSource）
                _nodeResultManager.UpdateNodeResult(e.Node, e.Result);

                // ★ 更新所有依赖此节点的数据提供者（修复前置节点未执行问题）
                UpdateDataProvidersForNode(e.Node, e.Result);

                // ★ 更新数据分析控件（任何节点执行完成后刷新）
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
        /// 更新所有依赖此节点的数据提供者
        /// </summary>
        private void UpdateDataProvidersForNode(Models.WorkflowNode node, Plugin.SDK.Execution.Results.ToolResults? result)
        {
            if (result == null || !result.IsSuccess) return;

            try
            {
                // 获取节点的输出图像
                var outputValues = result.GetOutputValues();
                if (outputValues == null || outputValues.Count == 0)
                {
                    AddLog($"⚠️ 节点 {node.Name} 无输出值");
                    return;
                }

                // 尝试找到图像类型的输出
                var imageOutput = outputValues.FirstOrDefault(kv =>
                    kv.Value is OpenCvSharp.Mat ||
                    kv.Key.Contains("Image", StringComparison.OrdinalIgnoreCase) ||
                    kv.Key.Contains("Output", StringComparison.OrdinalIgnoreCase));

                object? outputValue = imageOutput.Value ?? outputValues.FirstOrDefault(kv => kv.Value != null).Value;
                if (outputValue == null)
                {
                    AddLog($"⚠️ 节点 {node.Name} 输出值为空");
                    return;
                }

                // ★ 关键日志：输出值类型
                AddLog($"📤 节点 {node.Name} 输出类型: {outputValue.GetType().Name}");

                // 更新所有缓存的数据提供者
                int updatedCount = 0;
                foreach (var kvp in _nodeDataProviders.ToList())
                {
                    try
                    {
kvp.Value.UpdateNodeOutput(node.Id, "OutputValue", outputValue);
                        updatedCount++;
                    }
                    catch (Exception ex)
                    {
}
                }
                if (updatedCount > 0)
                {
                    AddLog($"🔄 已更新 {updatedCount} 个数据提供者");
                }
            }
            catch (Exception ex)
            {
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
        /// 浏览图像文件
        /// </summary>
        private void ExecuteBrowseImage()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图像文件|*.jpg;*.jpeg;*.png;*.bmp;*.tiff|所有文件|*.*",
                    Title = "选择图像文件"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    AddLog($"📁 选择文件: {filePath}");

                    // TODO: 加载图像到OriginalImage
                    // OriginalImage = LoadImageFromFile(filePath);
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ 加载图像失败: {ex.Message}");
                System.Windows.MessageBox.Show($"加载图像失败: {ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 加载图像
        /// </summary>
        private void ExecuteLoadImage()
        {
            try
            {
                if (OriginalImage == null)
                {
                    AddLog("❌ 请先选择图像文件");
                    return;
                }

                AddLog("✅ 加载图像成功");
                // TODO: 处理图像并更新ProcessedImage和ResultImage
            }
            catch (Exception ex)
            {
                AddLog($"❌ 加载图像失败: {ex.Message}");
                System.Windows.MessageBox.Show($"加载图像失败: {ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清除图像
        /// </summary>
        private void ExecuteClearImage()
        {
            try
            {
                OriginalImage = null;
                ProcessedImage = null;
                ResultImage = null;
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
                OriginalImage = null;
                ProcessedImage = null;
                ResultImage = null;
                return;
            }

            var imageInfo = ImageCollection[CurrentImageIndex];

            // 获取FullImage
            var fullImage = imageInfo.FullImage;

            if (fullImage != null)
            {
                OriginalImage = fullImage;
                AddLog($"📷 显示图像: {imageInfo.Name}");

                // 确保DisplayImage更新
                UpdateDisplayImage();
            }
        }

        #endregion

        #region 显示

        /// <summary>
        /// 更新显示图像
        /// </summary>
        private void UpdateDisplayImage()
        {
            if (SelectedImageType == null)
            {
                return;
            }

            switch (SelectedImageType.Type)
            {
                case ImageDisplayType.Original:
                    DisplayImage = OriginalImage;
                    break;
                case ImageDisplayType.Processed:
                    DisplayImage = ProcessedImage;
                    break;
                case ImageDisplayType.Result:
                    DisplayImage = ResultImage;
                    break;
            }
        }

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
        /// 清空计算结果面板
        /// </summary>
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
}
