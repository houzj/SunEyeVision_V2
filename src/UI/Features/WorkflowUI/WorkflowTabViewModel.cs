using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using AppCommands = SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Services.Workflow;


using SunEyeVision.UI.Core;

using SunEyeVision.UI.Features.WorkflowUI;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.WorkflowUI
{
    /// <summary>
    /// 工作流标签页ViewModel
    /// </summary>
    public class WorkflowTabViewModel : ObservableObject
    {
        private string _id;
        private string _name;
        private bool _isRunning;
        private RunMode _runMode;
        private WorkflowState _state;
        private ObservableCollection<WorkflowNodeBase> _workflowNodes;
        private ObservableCollection<WorkflowConnection> _workflowConnections;
        private ScaleTransform _scaleTransform;
        private double _currentScale;
        private CanvasType _canvasType;

        // ========== 子程序导航栈 ==========

        /// <summary>
        /// 导航栈，仅保存 workflowId 字符串
        /// SSOT 原则：引擎中的 Workflow 是唯一数据源
        /// </summary>
        private readonly Stack<string> _navigationStack = new();

        /// <summary>
        /// 当前显示的工作流ID（下钻时指向子程序工作流，返回后指回主工作流）
        /// </summary>
        private string _currentWorkflowId;

        /// <summary>
        /// 当前显示的工作流名称（下钻时为子程序名，根层次为 Tab.Name）
        /// </summary>
        private string _currentWorkflowName;

        /// <summary>
        /// 当前子程序节点的工具图标（Tab 栏设置按钮显示用，与画布节点图标一致）
        /// </summary>
        private string? _subroutineIcon;

        /// <summary>
        /// 当前子程序节点的工具图标（Tab 栏设置按钮显示用）
        /// 非子程序模式下为 null，设置按钮隐藏
        /// </summary>
        public string? SubroutineIcon
        {
            get => _subroutineIcon;
            set => SetProperty(ref _subroutineIcon, value);
        }

        /// <summary>
        /// 是否处于子程序下钻模式
        /// </summary>
        public bool IsInSubroutine => _navigationStack.Count > 0;

        /// <summary>Nearest parent first; callers receive a snapshot of navigation context.</summary>
        public IReadOnlyList<string> NavigationAncestorIds => _navigationStack.ToArray();

        /// <summary>
        /// 当前有效显示名称（主工作流显示 Name，子程序显示子程序名）
        /// </summary>
        public string EffectiveDisplayName => _currentWorkflowName;

        /// <summary>
        /// 当前工作流ID（下钻时指向子程序工作流，返回后指回主工作流）
        /// </summary>
        public string CurrentWorkflowId => _currentWorkflowId;

        /// <summary>
        /// Tab 根工作流ID（Tab 创建时确定，永不改变）
        /// 执行引擎始终使用此 ID 定位根工作流
        /// </summary>
        public string RootWorkflowId => _id;

        /// <summary>
        /// 节点序号管理器
        /// </summary>
        private readonly INodeSequenceManager _sequenceManager;

        /// <summary>
        /// 节点序号管理器（公共访问）
        /// </summary>
        public INodeSequenceManager SequenceManager => _sequenceManager;

        /// <summary>
        /// 节点工厂
        /// </summary>
        private readonly IWorkflowNodeFactory _nodeFactory;

        /// <summary>
        /// 节点工厂（公共访问）
        /// </summary>
        public IWorkflowNodeFactory WorkflowNodeFactory => _nodeFactory;

        /// <summary>
        /// 命令管理器
        /// </summary>
        public AppCommands.CommandManager CommandManager { get; private set; }

        /// <summary>
        /// 画布内容变更事件 — SetNodes/SetConnections 整体替换集合引用时触发。
        /// 所有依赖画布数据的组件（小地图、连接线系统等）统一订阅此事件，
        /// 集合替换后重绑，避免各自监听 PropertyChanged 造成遗漏。
        /// </summary>
        public event EventHandler? WorkflowContentChanged;

        public WorkflowTabViewModel() : this(new NodeSequenceManager())
        {
        }

        /// <summary>
        /// 带依赖注入的构造函数
        /// </summary>
        public WorkflowTabViewModel(INodeSequenceManager sequenceManager)
        {
            _sequenceManager = sequenceManager ?? throw new ArgumentNullException(nameof(sequenceManager));

            // 初始化节点工厂
            _nodeFactory = new WorkflowNodeFactory(_sequenceManager);

            _id = Guid.NewGuid().ToString();
            _currentWorkflowId = _id;
            _currentWorkflowName = "工作流"; // 默认名称，可以被外部覆盖
            _name = "工作流"; // 默认名称，可以被外部覆盖
            State = WorkflowState.Stopped;
            RunMode = RunMode.Single;
            _workflowNodes = new ObservableCollection<WorkflowNodeBase>();
            _workflowConnections = new ObservableCollection<WorkflowConnection>();
            CurrentScale = 1.0;
            ScaleTransform = new ScaleTransform(1.0, 1.0);
            CanvasType = CanvasType.WorkflowCanvas;

            // 初始化命令管理器
            CommandManager = new CommandManager(_workflowNodes, _workflowConnections);

            // 订阅节点集合变化事件（仅用于索引释放）
            _workflowNodes.CollectionChanged += OnWorkflowNodesChanged;
            _workflowConnections.CollectionChanged += (s, e) => { };
        }

        /// <summary>
        /// 唯一标识ID（也是 RootWorkflowId）
        /// </summary>
        public string Id
        {
            get => _id;
            set
            {
                if (SetProperty(ref _id, value))
                {
                    // RootWorkflowId 跟随 Id
                    _currentWorkflowId = value;
                    OnPropertyChanged(nameof(RootWorkflowId));
                    OnPropertyChanged(nameof(CurrentWorkflowId));
                }
            }
        }

        /// <summary>
        /// 标签页名称（根工作流名称）
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    _currentWorkflowName = value;
                    OnPropertyChanged(nameof(EffectiveDisplayName));
                }
            }
        }

        #region 行内重命名

        private bool _isEditingName;
        private string _renameBuffer = string.Empty;

        /// <summary>
        /// 重命名完成事件 — 通知外部（如 MainWindowViewModel）更新引擎中的 Workflow.Name
        /// </summary>
        public event EventHandler<string>? NameCommitted;

        /// <summary>
        /// 是否正在编辑名称
        /// </summary>
        public bool IsEditingName
        {
            get => _isEditingName;
            set => SetProperty(ref _isEditingName, value);
        }

        /// <summary>
        /// 编辑中的临时名称缓冲
        /// </summary>
        public string RenameBuffer
        {
            get => _renameBuffer;
            set => SetProperty(ref _renameBuffer, value);
        }

        /// <summary>
        /// 开始重命名（复制当前显示名称到缓冲）
        /// 子程序下钻时显示子程序名，根层次显示 Tab.Name
        /// </summary>
        public void BeginRename()
        {
            RenameBuffer = EffectiveDisplayName;
            IsEditingName = true;
        }

        /// <summary>
        /// 确认重命名 — 写入引擎中当前工作流的 Name
        /// 触发 NameCommitted 事件通知外部更新引擎
        /// </summary>
        public bool ConfirmRename()
        {
            var trimmed = RenameBuffer?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                IsEditingName = false;
                return false;
            }

            // 更新当前显示名称
            _currentWorkflowName = trimmed;
            if (!IsInSubroutine)
            {
                _name = trimmed;
                OnPropertyChanged(nameof(Name));
            }
            OnPropertyChanged(nameof(EffectiveDisplayName));
            IsEditingName = false;

            // 通知外部更新引擎中的 Workflow.Name
            NameCommitted?.Invoke(this, trimmed);
            return true;
        }

        /// <summary>
        /// 取消重命名
        /// </summary>
        public void CancelRename()
        {
            IsEditingName = false;
        }

        #endregion

        /// <summary>
        /// 设置当前显示的工作流名称（由外部在导航后调用）
        /// </summary>
        public void SetCurrentWorkflowName(string name)
        {
            _currentWorkflowName = name;
            OnPropertyChanged(nameof(EffectiveDisplayName));
        }

        /// <summary>
        /// 是否正在运行
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    State = value ? WorkflowState.Running : WorkflowState.Stopped;
                }
            }
        }

        /// <summary>
        /// 运行模式
        /// </summary>
        public RunMode RunMode
        {
            get => _runMode;
            set => SetProperty(ref _runMode, value);
        }

        /// <summary>
        /// 工作流状态
        /// </summary>
        public WorkflowState State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        /// <summary>
        /// 节点集合
        /// </summary>
        public ObservableCollection<WorkflowNodeBase> WorkflowNodes
        {
            get => _workflowNodes;
            set
            {
                if (_workflowNodes != null)
                {
                    _workflowNodes.CollectionChanged -= OnWorkflowNodesChanged;
                }
                SetProperty(ref _workflowNodes, value);
                if (_workflowNodes != null)
                {
                    _workflowNodes.CollectionChanged += OnWorkflowNodesChanged;
                }
            }
        }

        private void OnWorkflowNodesChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 处理删除节点，释放索引到空洞池
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                if (e.OldItems != null)
                {
                    foreach (SunEyeVision.UI.Shared.WorkflowNode node in e.OldItems)
                    {
                        try
                        {
                            if (node.LocalIndex >= 0)
                            {
                                _sequenceManager.ReleaseLocalIndex(CurrentWorkflowId, node.ToolType ?? string.Empty, node.LocalIndex);
                                VisionLogger.Instance.Log(
                                    LogLevel.Info,
                                    $"节点删除，释放索引: {node.LocalIndex}, 节点ID: {node.Id}",
                                    "WorkflowTabViewModel"
                                );
                            }

                            if (node.GlobalIndex >= 0)
                            {
                                _sequenceManager.ReleaseGlobalIndex(node.GlobalIndex);
                                VisionLogger.Instance.Log(
                                    LogLevel.Info,
                                    $"节点删除，释放全局索引: {node.GlobalIndex}, 节点ID: {node.Id}",
                                    "WorkflowTabViewModel"
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            VisionLogger.Instance.Log(
                                LogLevel.Error,
                                $"释放索引失败: {node.Id}, 错误: {ex.Message}",
                                "WorkflowTabViewModel",
                                ex
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 设置连接集合（直接绑定外部集合，实现自动同步）
        /// </summary>
        public void SetConnections(ObservableCollection<WorkflowConnection> connections)
        {
            if (connections == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }

            _workflowConnections.CollectionChanged -= (s, e) => { };
            _workflowConnections = connections;
            _workflowConnections.CollectionChanged += (s, e) => { };

            OnPropertyChanged(nameof(WorkflowConnections));
            CommandManager = new CommandManager(_workflowNodes, _workflowConnections);
            WorkflowContentChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 连接线集合
        /// </summary>
        public ObservableCollection<WorkflowConnection> WorkflowConnections
        {
            get => _workflowConnections;
            set
            {
                if (_workflowConnections != null)
                {
                    _workflowConnections.CollectionChanged -= (s, e) => { };
                }
                SetProperty(ref _workflowConnections, value);
                if (_workflowConnections != null)
                {
                    _workflowConnections.CollectionChanged += (s, e) => { };
                }
            }
        }

        /// <summary>
        /// 设置节点集合 — 直接绑定引擎中的同一对象实例（单一数据源）
        /// 与 SetConnections 对齐：UI 与引擎共享同一集合，节点增删自动双向同步。
        /// </summary>
        public void SetNodes(ObservableCollection<WorkflowNodeBase> nodes)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));

            // 直接赋值引擎工作流的节点集合，消除 UI/引擎两套集合的同步问题。
            if (!ReferenceEquals(_workflowNodes, nodes))
            {
                _workflowNodes.CollectionChanged -= OnWorkflowNodesChanged;
                _workflowNodes = nodes;
                _workflowNodes.CollectionChanged += OnWorkflowNodesChanged;
            }

            OnPropertyChanged(nameof(WorkflowNodes));
            CommandManager = new CommandManager(_workflowNodes, _workflowConnections);
            WorkflowContentChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 缩放变换（用于画布缩放）
        /// </summary>
        public ScaleTransform ScaleTransform
        {
            get => _scaleTransform;
            set => SetProperty(ref _scaleTransform, value);
        }

        /// <summary>
        /// 当前缩放比例（默认1.0即100%）
        /// </summary>
        public double CurrentScale
        {
            get => _currentScale;
            set => SetProperty(ref _currentScale, value);
        }

        /// <summary>
        /// 画布类型
        /// </summary>
        public CanvasType CanvasType
        {
            get => _canvasType;
            set => SetProperty(ref _canvasType, value);
        }

        /// <summary>
        /// 单次运行按钮文本
        /// </summary>
        public string SingleRunButtonText => "▶ 运行";

        /// <summary>
        /// 连续运行按钮文本
        /// </summary>
        public string ContinuousRunButtonText => IsRunning ? "⏹ 停止" : "⏵ 连续";

        /// <summary>
        /// 是否可关闭
        /// </summary>
        public bool IsCloseable => true;

        /// <summary>
        /// 获取状态显示文本
        /// </summary>
        public string StateText
        {
            get
            {
                return State switch
                {
                    WorkflowState.Stopped => "已停止",
                    WorkflowState.Running => "运行中",
                    WorkflowState.Paused => "已暂停",
                    WorkflowState.Error => "错误",
                    _ => "未知"
                };
            }
        }

        /// <summary>
        /// 获取状态颜色
        /// </summary>
        public string StateColor
        {
            get
            {
                return State switch
                {
                    WorkflowState.Stopped => "#999999",
                    WorkflowState.Running => "#00CC99",
                    WorkflowState.Paused => "#FF9900",
                    WorkflowState.Error => "#FF4444",
                    _ => "#999999"
                };
            }
        }

        /// <summary>
        /// 创建新节点并添加到工作流
        /// </summary>
        /// <param name="algorithmType">算法类型</param>
        /// <param name="name">节点名称（可选，默认使用算法名称）</param>
        /// <returns>新创建的节点</returns>
        public SunEyeVision.UI.Shared.WorkflowNode CreateNode(string algorithmType, string? name = null)
        {
            if (_nodeFactory == null)
            {
                throw new InvalidOperationException("NodeFactory is not initialized");
            }

            // ✅ 直接使用工厂创建节点，内部会自动获取局部索引（优先从空洞池获取）
            // ★ 用 CurrentWorkflowId 而非 Id：子程序下钻时，新节点局部索引须归属当前子程序工作流，
            //   否则与主工作流同类型节点共享序号、跨画布重名冲突。
            var node = _nodeFactory.CreateNode(algorithmType, name, CurrentWorkflowId);

            VisionLogger.Instance.Log(
                LogLevel.Info,
                $"创建节点: {node.Id}, 算法类型: {algorithmType}",
                "WorkflowTabViewModel"
            );

            return node;
        }

        /// <summary>
        /// 释放节点的局部序号到空洞池（节点重命名时使用）
        /// </summary>
        public void ReleaseNodeLocalIndex(WorkflowNodeBase node)
        {
            if (node == null || node.LocalIndex < 0)
                return;

            // ★ 用 CurrentWorkflowId 而非 Id：与创建节点(CreateNode)的归属工作流保持一致，
            //   确保子程序内节点重命名释放的序号进入正确的子程序上下文，不污染主工作流空洞池。
            _sequenceManager.ReleaseLocalIndex(CurrentWorkflowId, node.ToolType ?? string.Empty, node.LocalIndex);

            VisionLogger.Instance.Log(
                LogLevel.Info,
                $"节点重命名，释放局部序号: {node.LocalIndex}, 节点ID: {node.Id}",
                "WorkflowTabViewModel"
            );

            node.LocalIndex = -1;
        }

        /// <summary>
        /// 重置节点序号
        /// </summary>
        public void ResetNodeSequences()
        {
            _sequenceManager.ResetWorkflow(Id);
            VisionLogger.Instance.Log(
                LogLevel.Info,
                "重置节点序号",
                "WorkflowTabViewModel"
            );
        }

        /// <summary>
        /// 强制刷新属性（用于手动更新）
        /// </summary>
        public void RefreshProperty(string propertyName)
        {
            base.OnPropertyChanged(propertyName);
        }

        // ========== 子程序导航栈方法 ==========

        /// <summary>
        /// 下钻到子程序
        /// </summary>
        /// <param name="subroutineWorkflowId">子程序工作流ID</param>
        /// <param name="displayName">Tab显示名称</param>
        /// <param name="subroutineIcon">子程序节点的工具图标（Tab 栏设置按钮显示用）</param>
        public void NavigateToSubroutine(string subroutineWorkflowId, string displayName, string? subroutineIcon = null)
        {
            // 保存当前工作流ID到栈
            _navigationStack.Push(_currentWorkflowId);

            // 切换到子程序工作流
            _currentWorkflowId = subroutineWorkflowId;
            _currentWorkflowName = displayName;
            SubroutineIcon = subroutineIcon;

            VisionLogger.Instance?.Log(LogLevel.Info,
                $"子程序下钻: workflowId={subroutineWorkflowId}, displayName={displayName}, subroutineIcon={(string.IsNullOrEmpty(subroutineIcon) ? "(空)" : $"'{subroutineIcon}' ({subroutineIcon.Length}字符)")}",
                "WorkflowTabViewModel");

            // 通知UI更新
            OnPropertyChanged(nameof(IsInSubroutine));
            OnPropertyChanged(nameof(EffectiveDisplayName));
            OnPropertyChanged(nameof(CurrentWorkflowId));
        }

        /// <summary>
        /// 返回上一层
        /// </summary>
        /// <returns>是否有更高层级可返回</returns>
        public bool NavigateBack()
        {
            if (_navigationStack.Count == 0) return false;

            // 弹出上一级工作流ID
            _currentWorkflowId = _navigationStack.Pop();

            // _currentWorkflowName 由外部在调用 SetCurrentWorkflowName 时设置
            SubroutineIcon = null;
            OnPropertyChanged(nameof(IsInSubroutine));
            OnPropertyChanged(nameof(EffectiveDisplayName));
            OnPropertyChanged(nameof(CurrentWorkflowId));

            return true;
        }

        /// <summary>
        /// 重置导航栈（返回主工作流顶层）
        /// </summary>
        public void ResetNavigation()
        {
            _navigationStack.Clear();
            _currentWorkflowId = _id;
            _currentWorkflowName = _name;
            SubroutineIcon = null;

            OnPropertyChanged(nameof(IsInSubroutine));
            OnPropertyChanged(nameof(EffectiveDisplayName));
            OnPropertyChanged(nameof(CurrentWorkflowId));
        }

        /// <summary>
        /// 获取调用链上所有层级的显示名称（从顶层到底层）
        /// </summary>
        public string[] GetNavigationPath()
        {
            var path = new List<string>();
            var frames = _navigationStack.ToArray();
            for (int i = frames.Length - 1; i >= 0; i--)
            {
                path.Add(frames[i]); // 现在只是 workflowId，暂时用 ID 代替，调用方可决定是否显示
            }
            path.Add(_currentWorkflowName);
            return path.ToArray();
        }
    }

    /// <summary>
    /// 工作流状态枚举
    /// </summary>
    public enum WorkflowState
    {
        Stopped,   // 已停止
        Running,   // 运行中
        Paused,    // 已暂停
        Error      // 错误
    }
}
