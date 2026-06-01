using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using AppCommands = SunEyeVision.UI.Commands;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Commands;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Models;
using SunEyeVision.UI.Services;
using SunEyeVision.UI.Views.Controls.Canvas;
using SunEyeVision.UI.Services.Interaction;
using SunEyeVision.UI.Services.Workflow;


namespace SunEyeVision.UI.ViewModels
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
        private ObservableCollection<Models.WorkflowNode> _workflowNodes;
        private ObservableCollection<WorkflowConnection> _workflowConnections;
        private ScaleTransform _scaleTransform;
        private double _currentScale;
        private CanvasType _canvasType;

        // ========== 子程序导航栈 ==========

        /// <summary>
        /// 导航栈，保存各级子程序/主工作流的画布状态
        /// </summary>
        private readonly Stack<NavigationFrame> _navigationStack = new();

        /// <summary>
        /// 当前帧（当前显示的层级）
        /// </summary>
        private NavigationFrame? _currentFrame;

        /// <summary>
        /// 是否已初始化导航（第一次设置画布状态后为 true）
        /// </summary>
        private bool _isNavigationInitialized;

        /// <summary>
        /// 是否处于子程序下钻模式
        /// </summary>
        public bool IsInSubroutine => _currentFrame != null && _navigationStack.Count > 0;

        /// <summary>
        /// 当前有效显示名称（主工作流显示 Name，子程序显示子程序名）
        /// </summary>
        public string EffectiveDisplayName => _currentFrame?.DisplayName ?? Name;

        /// <summary>
        /// 当前工作流ID（下钻时指向子程序工作流，返回后指回主工作流）
        /// </summary>
        public string CurrentWorkflowId => _currentFrame?.WorkflowId ?? Id;

        /// <summary>
        /// 外部节点集合引用（通常是 Solution.Workflow.Nodes），用于反向同步
        /// Phase 3: UI 侧删除/添加节点时自动同步到数据层
        /// </summary>
        private ObservableCollection<WorkflowNodeBase>? _externalNodes;

        /// <summary>
        /// 反向同步重入锁，防止双向同步死循环
        /// </summary>
        private bool _isReverseSyncing;

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

            Id = Guid.NewGuid().ToString();
            Name = "工作流"; // 默认名称，可以被外部覆盖
            State = WorkflowState.Stopped;
            RunMode = RunMode.Single;
            WorkflowNodes = new ObservableCollection<Models.WorkflowNode>();
            // ✅ 优化：创建临时的连接集合，稍后通过 SetConnections 方法绑定外部集合
            _workflowConnections = new ObservableCollection<WorkflowConnection>();
            CurrentScale = 1.0;
            ScaleTransform = new ScaleTransform(1.0, 1.0);
            CanvasType = CanvasType.WorkflowCanvas; // 默认使用 WorkflowCanvas，每个工作流独立

            // 初始化命令管理器（使用临时连接集合）
            CommandManager = new CommandManager(WorkflowNodes, _workflowConnections);

            // 订阅节点和连接集合变化事件
            WorkflowNodes.CollectionChanged += (s, e) => OnWorkflowNodesChanged(s, e);
            _workflowConnections.CollectionChanged += (s, e) => OnWorkflowConnectionsChanged(s, e);
        }

        /// <summary>
        /// 唯一标识ID
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 标签页名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        #region 行内重命名

        private bool _isEditingName;
        private string _renameBuffer = string.Empty;

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
        /// 开始重命名（将当前名称复制到缓冲）
        /// </summary>
        public void BeginRename()
        {
            RenameBuffer = Name;
            IsEditingName = true;
        }

        /// <summary>
        /// 确认重命名
        /// </summary>
        public bool ConfirmRename()
        {
            var trimmed = RenameBuffer?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                IsEditingName = false;
                return false;
            }

            if (_name != trimmed)
            {
                Name = trimmed;
            }
            IsEditingName = false;
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
        public ObservableCollection<Models.WorkflowNode> WorkflowNodes
        {
            get => _workflowNodes;
            set
            {
                if (_workflowNodes != null)
                {
                    _workflowNodes.CollectionChanged -= (s, e) => OnWorkflowNodesChanged(s, e);
                }
                SetProperty(ref _workflowNodes, value);
                if (_workflowNodes != null)
                {
                    _workflowNodes.CollectionChanged += (s, e) => OnWorkflowNodesChanged(s, e);
                }
            }
        }

        private void OnWorkflowNodesChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Phase 3: 反向同步 - UI 集合变更时同步到外部集合（Solution.Workflow.Nodes）
            if (!_isReverseSyncing && _externalNodes != null)
            {
                _isReverseSyncing = true;
                try
                {
                    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
                    {
                        foreach (WorkflowNodeBase node in e.OldItems)
                        {
                            if (_externalNodes.Contains(node))
                            {
                                _externalNodes.Remove(node);
                                VisionLogger.Instance.Log(
                                    LogLevel.Info,
                                    $"反向同步：从数据层删除节点: {node.Id}",
                                    "WorkflowTabViewModel"
                                );
                            }
                        }
                    }
                    else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
                    {
                        foreach (WorkflowNodeBase node in e.NewItems)
                        {
                            if (!_externalNodes.Contains(node))
                            {
                                _externalNodes.Add(node);
                                VisionLogger.Instance.Log(
                                    LogLevel.Info,
                                    $"反向同步：添加到数据层节点: {node.Id}",
                                    "WorkflowTabViewModel"
                                );
                            }
                        }
                    }
                }
                finally
                {
                    _isReverseSyncing = false;
                }
            }

            // 处理删除节点，释放索引到空洞池
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                if (e.OldItems != null)
                {
                    foreach (Models.WorkflowNode node in e.OldItems)
                    {
                        try
                        {
                            // 直接使用LocalIndex属性释放索引到空洞池
                            if (node.LocalIndex >= 0)
                            {
                                _sequenceManager.ReleaseLocalIndex(Id, node.ToolType ?? string.Empty, node.LocalIndex);

                                VisionLogger.Instance.Log(
                                    LogLevel.Info,
                                    $"节点删除，释放索引: {node.LocalIndex}, 节点ID: {node.Id}",
                                    "WorkflowTabViewModel"
                                );
                            }

                            // 释放全局索引到空洞池
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
        /// <param name="connections">外部连接集合（通常是 Solution.Workflow.Connections）</param>
        /// <remarks>
        /// 注意：此方法会替换内部的连接集合引用，所有对连接的操作都会直接影响外部集合
        /// </remarks>
        public void SetConnections(ObservableCollection<WorkflowConnection> connections)
        {
            if (connections == null)
            {
                throw new ArgumentNullException(nameof(connections));
            }

            // 备份当前集合中的连接（如果有）
            var tempConnections = new List<WorkflowConnection>(_workflowConnections);

            // 取消旧集合的事件订阅
            _workflowConnections.CollectionChanged -= (s, e) => OnWorkflowConnectionsChanged(s, e);

            // ✅ 优化：直接绑定外部集合，实现自动同步
            _workflowConnections = connections;

            // 订阅新集合的事件
            _workflowConnections.CollectionChanged += (s, e) => OnWorkflowConnectionsChanged(s, e);

            // 通知属性变化
            OnPropertyChanged(nameof(WorkflowConnections));

            // ✅ 由于 CommandManager 持有的是旧集合的引用，我们需要重新创建 CommandManager
            // TODO: 考虑优化 CommandManager 设计，支持动态更新集合引用
            CommandManager = new CommandManager(WorkflowNodes, _workflowConnections);
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
                    _workflowConnections.CollectionChanged -= (s, e) => OnWorkflowConnectionsChanged(s, e);
                }
                SetProperty(ref _workflowConnections, value);
                if (_workflowConnections != null)
                {
                    _workflowConnections.CollectionChanged += (s, e) => OnWorkflowConnectionsChanged(s, e);
                }
            }
        }

        /// <summary>
        /// 设置节点集合（直接绑定外部集合中的相同实例，实现对象级共享）
        /// </summary>
        /// <param name="nodes">外部节点集合（通常是 Solution.Workflow.Nodes）</param>
        /// <remarks>
        /// Phase 2 优化：
        /// - 将外部集合中的节点引用直接添加到内部集合（同一对象实例）
        /// - 订阅外部集合的 CollectionChanged 事件，实现自动同步
        /// - 外部集合中的节点必须已经是 WorkflowNode 实例
        /// </remarks>
        public void SetNodes(ObservableCollection<WorkflowNodeBase> nodes)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));

            // 保存外部集合引用，用于反向同步
            _externalNodes = nodes;

            // 取消外部集合的事件订阅
            nodes.CollectionChanged -= OnExternalNodesCollectionChanged;

            // 清空并填充（同一对象引用）
            _workflowNodes.Clear();
            foreach (var node in nodes)
            {
                if (node is Models.WorkflowNode uiNode)
                {
                    _workflowNodes.Add(uiNode);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"所有节点必须是 WorkflowNode 实例。节点 {node.Id} 的实际类型为 {node.GetType().Name}");
                }
            }

            // 订阅外部集合的变化事件
            nodes.CollectionChanged += OnExternalNodesCollectionChanged;

            // 通知属性变化
            OnPropertyChanged(nameof(WorkflowNodes));

            // 重新创建 CommandManager（持有新的集合引用）
            CommandManager = new CommandManager(_workflowNodes, _workflowConnections);
        }

        private void OnExternalNodesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Phase 3: 反向同步重入锁 - 如果变更来自反向同步，跳过避免死循环
            if (_isReverseSyncing)
                return;

            // 当外部集合（workflow.Nodes）变化时，同步到内部集合
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (WorkflowNodeBase node in e.NewItems)
                {
                    if (node is Models.WorkflowNode uiNode && !_workflowNodes.Contains(uiNode))
                    {
                        _workflowNodes.Add(uiNode);
                    }
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (WorkflowNodeBase node in e.OldItems)
                {
                    if (node is Models.WorkflowNode uiNode)
                    {
                        _workflowNodes.Remove(uiNode);
                    }
                }
            }
        }

        private void OnWorkflowConnectionsChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
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
        public Models.WorkflowNode CreateNode(string algorithmType, string? name = null)
        {
            if (_nodeFactory == null)
            {
                throw new InvalidOperationException("NodeFactory is not initialized");
            }

            // ✅ 直接使用工厂创建节点，内部会自动获取局部索引（优先从空洞池获取）
            var node = _nodeFactory.CreateNode(algorithmType, name, Id);

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

            _sequenceManager.ReleaseLocalIndex(Id, node.ToolType ?? string.Empty, node.LocalIndex);

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
        /// 初始化导航（在 SetNodes 后由外部调用一次）
        /// </summary>
        public void InitializeNavigation()
        {
            if (_isNavigationInitialized) return;
            _currentFrame = new NavigationFrame
            {
                DisplayName = Name,
                WorkflowId = Id,
                Nodes = WorkflowNodes,
                Connections = WorkflowConnections,
                CurrentScale = CurrentScale
            };
            _isNavigationInitialized = true;
        }

        /// <summary>
        /// 是否已初始化导航栈
        /// </summary>
        public bool IsNavigationInitialized => _isNavigationInitialized;

        /// <summary>
        /// 下钻到子程序
        /// </summary>
        /// <param name="subroutineId">子程序工作流ID</param>
        /// <param name="displayName">Tab显示名称</param>
        /// <param name="callerNodeId">调用此子程序的节点ID</param>
        public void NavigateToSubroutine(string subroutineId, string displayName, string callerNodeId)
        {
            if (!_isNavigationInitialized)
                throw new InvalidOperationException("请先调用 InitializeNavigation()");

            // 1. 更新当前帧的缩放状态
            if (_currentFrame != null)
            {
                _currentFrame.CurrentScale = CurrentScale;
            }

            // 2. 保存当前帧到栈
            _navigationStack.Push(_currentFrame!);

            // 3. 创建子程序帧（节点/连线稍后由外部填充）
            _currentFrame = new NavigationFrame
            {
                DisplayName = displayName,
                WorkflowId = subroutineId,
                CallerNodeId = callerNodeId,
                CurrentScale = 1.0
            };

            // 4. 通知UI更新
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

            // 1. 恢复上一帧
            _currentFrame = _navigationStack.Pop();

            // 2. 通知UI更新
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
            if (_currentFrame != null)
            {
                _currentFrame = new NavigationFrame
                {
                    DisplayName = Name,
                    WorkflowId = Id,
                    Nodes = WorkflowNodes,
                    Connections = WorkflowConnections,
                    CurrentScale = CurrentScale
                };
            }
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
                path.Add(frames[i].DisplayName);
            }
            if (_currentFrame != null)
            {
                path.Add(_currentFrame.DisplayName);
            }
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
