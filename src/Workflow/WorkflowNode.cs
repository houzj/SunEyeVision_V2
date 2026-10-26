using System.Linq;
﻿using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Models;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 工作流节点基类
    /// </summary>
    /// <remarks>
    /// 优化说明（2026-03-26 重构）：
    /// - 继承 ObservableObject，支持属性通知
    /// - UI 层专有属性使用 [JsonIgnore] 标记，不参与序列化
    /// - 消除 UI.Models.WorkflowNode 副本，直接使用此类
    /// - 单一数据源：Solution.Workflow.Nodes 直接被 UI 绑定
    ///
    /// 核心字段（序列化）：
    /// - Id, Name, DispName, ToolType
    /// - LocalIndex, GlobalIndex
    /// - Parameters, ParamSettings
    /// - IsEnabled, PositionX, PositionY, Width, Height
    ///
    /// UI 专有属性（不序列化）：
    /// - IsSelected, IsVisible, Status
    /// - OutputCache, InputSource, LastResult
    /// - StyleConfig (存储为基本类型)
    /// </remarks>
    public class WorkflowNodeBase : ObservableObject
    {
    [System.Text.Json.Serialization.JsonExtensionData]
    public System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }

        #region 核心字段（9个）

        /// <summary>
        /// 子程序关联ID（当 ToolType == "Subroutine" 时有效）
        /// 指向 WorkflowEngine 中一个独立的 Workflow
        /// </summary>
        public string? SubroutineId { get; set; }

        /// <summary>
        /// 节点ID - 使用Guid确保全局唯一性和稳定性
        /// </summary>
        /// <remarks>
        /// 节点ID使用Guid,避免因显示名称修改导致ID变更的风险。
        /// 示例：e2a8c5d7-f3e2-4b1a-9c5d-8f7e6b5a4c3d
        /// </remarks>
        public string Id { get; set; }

        /// <summary>
        /// 全局序号 - 跨所有工作流共享的全局唯一递增序号
        /// </summary>
        /// <remarks>
        /// 全局序号用于标识节点创建的全局顺序，不会因为工作流切换而重新计算。
        /// 每次创建节点时递增，确保全局唯一性。
        /// </remarks>
        public int GlobalIndex { get; set; } = -1;

        /// <summary>
        /// 局部序号 - 同类型节点的局部序号,直接存储
        /// </summary>
        /// <remarks>
        /// 局部序号不再从节点ID解析,而是作为独立属性存储。
        /// 支持hole pool机制进行索引复用。
        /// </remarks>
        public int LocalIndex { get; set; } = -1;

        private string _name = string.Empty;
        private string _dispName = string.Empty;

        /// <summary>
        /// 节点名称 - 格式：{GlobalIndex} {DispName}
        /// </summary>
        /// <remarks>
        /// 节点名称由全局序号和显示名称组成,用于序列化。
        /// 示例：1 图像采集1, 2 高斯模糊2
        /// </remarks>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 节点显示名称 - 格式：{DisplayName}{LocalIndex}
        /// </summary>
        /// <remarks>
        /// 节点显示名称由工具显示名称和局部序号组成,用于UI显示。
        /// 示例：图像采集1, 高斯模糊2
        /// 支持用户重命名。
        /// </remarks>
        public string DispName
        {
            get => _dispName;
            set => SetProperty(ref _dispName, value);
        }

        /// <summary>
        /// 工具类型（算法类型的简化表示，符合视觉软件行业标准）
        /// </summary>
        /// <remarks>
        /// 用于标识节点使用的工具类型。
        /// NodeType 从此类型动态推断。
        /// </remarks>
        public string ToolType { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        private bool _isEnabled = true;
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

        private double _positionX = 0;
        /// <summary>
        /// 节点位置X坐标
        /// </summary>
        public double PositionX
        {
            get => _positionX;
            set
            {
                if (SetProperty(ref _positionX, value))
                {
                    OnPropertyChanged(nameof(Position));
                }
            }
        }

        private double _positionY = 0;
        /// <summary>
        /// 节点位置Y坐标
        /// </summary>
        public double PositionY
        {
            get => _positionY;
            set
            {
                if (SetProperty(ref _positionY, value))
                {
                    OnPropertyChanged(nameof(Position));
                }
            }
        }

        /// <summary>
        /// 节点宽度
        /// </summary>
        public double Width { get; set; } = 140;

        /// <summary>
        /// 节点高度
        /// </summary>
        public double Height { get; set; } = 90;

        /// <summary>
        /// 节点参数
        /// </summary>
        public ToolParameters Parameters { get; set; }

        /// <summary>
        /// 参数绑定配置
        /// </summary>
        /// <remarks>
        /// 支持参数与父节点输出的动态绑定。
        /// 用于在执行时自动从父节点获取参数值。
        /// </remarks>
        public ParamSettingContainer ParamSettings { get; set; }

        /// <summary>
        /// 用户为该节点选择的图像显示源（持久化保存）
        /// </summary>
        /// <remarks>
        /// 保存后重新打开工作流时，图像预览自动恢复到用户上次选择的显示源。
        /// 使用 ImageSourceReference 可序列化模型，仅存储身份+展示信息。
        /// 反序列化旧文件时此属性为 null，行为与之前一致（回退到默认）。
        /// </remarks>
        public ImageSourceReference? SelectedDisplaySource { get; set; }

        #endregion

        #region UI 层专有属性（不序列化）

        private bool _isSelected;
        private bool _isVisible = true;
        private string _status = "待运行";
        private object? _outputCache;
        private object? _inputSource;
        private object? _lastResult;
        private object? _styleConfig;
        private Type? _debugWindowType;
        private Type? _nodeStyleType;

        /// <summary>
        /// 节点位置（UI 层专用，不序列化，基于 PositionX/PositionY 计算）
        /// </summary>
        [JsonIgnore]
        public Point Position
        {
            get => new Point(PositionX, PositionY);
            set
            {
                if (PositionX != value.X || PositionY != value.Y)
                {
                    PositionX = value.X;
                    PositionY = value.Y;
                    OnPropertyChanged(nameof(Position));
                }
            }
        }

        /// <summary>
        /// 是否被选中（UI 层专用，不序列化）
        /// </summary>
        [JsonIgnore]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 节点是否可见（UI 层专用，用于虚拟化渲染，不序列化）
        /// </summary>
        [JsonIgnore]
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        /// <summary>
        /// 节点状态（UI 层专用，不序列化）
        /// </summary>
        [JsonIgnore]
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// 节点输出缓存（UI 层专用，不序列化）
        /// </summary>
        [JsonIgnore]
        public object? OutputCache
        {
            get => _outputCache;
            set => SetProperty(ref _outputCache, value);
        }

        /// <summary>
        /// 节点输入源（UI 层专用，不序列化）
        /// </summary>
        [JsonIgnore]
        public object? InputSource
        {
            get => _inputSource;
            set => SetProperty(ref _inputSource, value);
        }

        /// <summary>
        /// 图像输入源持久化配置（随方案文件保存/恢复）
        /// </summary>
        /// <remarks>
        /// 运行时 InputSource 为 UI 层 ImageInputSource（[JsonIgnore]，不序列化）。
        /// 此属性存路径与状态（可序列化，Workflow 层无 UI 依赖），
        /// 保存方案时 UI 层写入，打开方案时 UI 层据此重建 InputSource。
        /// </remarks>
        public ImageSourceConfig? ImageSourceConfig { get; set; }

        /// <summary>
        /// 最近一次执行结果（UI 层专用，不序列化）
        /// </summary>
        [JsonIgnore]
        public object? LastResult
        {
            get => _lastResult;
            set => SetProperty(ref _lastResult, value);
        }

        /// <summary>
        /// 节点样式配置（UI 层专用，不序列化）
        /// </summary>
        [JsonIgnore]
        public object? StyleConfig
        {
            get => _styleConfig;
            set => SetProperty(ref _styleConfig, value);
        }

        /// <summary>
        /// 调试窗口类型（UI 层专用，不序列化）
        /// </summary>
        /// <remarks>
        /// 从工具元数据传递而来，用于创建节点专用的调试窗口。
        /// </remarks>
        [JsonIgnore]
        public Type? DebugWindowType
        {
            get => _debugWindowType;
            set => SetProperty(ref _debugWindowType, value);
        }

        /// <summary>
        /// 调试窗口样式（UI 层专用，不序列化）
        /// </summary>
        /// <remarks>
        /// 从工具元数据传递而来，定义窗口打开行为。
        /// 窗口类型优先级：节点级配置 → 工具级配置 → 全局默认值
        /// </remarks>
        [JsonIgnore]
        public DebugWindowStyle DebugWindowStyle
        {
            get => _debugWindowStyle;
            set => SetProperty(ref _debugWindowStyle, value);
        }

        private DebugWindowStyle _debugWindowStyle = DebugWindowStyle.Default;

        /// <summary>
        /// 节点样式类型（UI 层专用，不序列化）
        /// </summary>
        /// <remarks>
        /// 从工具元数据传递而来，用于动态创建节点样式实例。
        /// </remarks>
        [JsonIgnore]
        public Type? NodeStyleType
        {
            get => _nodeStyleType;
            set => SetProperty(ref _nodeStyleType, value);
        }

        /// <summary>
        /// 上游节点池（按输出类型分组，UI 层专用，不序列化）
        /// </summary>
        /// <remarks>
        /// 存储当前节点的所有上游节点，按输出类型分组。
        /// 用于快速查找特定类型的上游节点。
        /// 
        /// 数据结构：
        /// - Key: 输出类型
        /// - Value: 拥有该输出类型的上游节点ID列表
        /// 
        /// 维护方式：
        /// - 连接建立时：增量添加上游节点
        /// - 连接删除时：增量移除上游节点
        /// </remarks>
        [JsonIgnore]
        public Dictionary<Type, List<string>> UpstreamNodesByOutputType { get; set; } = new Dictionary<Type, List<string>>();

        /// <summary>
        /// 获取节点样式配置实例（由 UI 层调用）
        /// </summary>
        /// <remarks>
        /// 根据 NodeStyleType 动态创建样式实例。
        /// 如果 NodeStyleType 为 null，UI 层应使用默认的 StandardNodeStyle。
        /// </remarks>
        /// <returns>节点样式实例，如果创建失败返回 null</returns>
        public object? GetStyleConfig()
        {
            try
            {
                if (_nodeStyleType == null)
                {
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"NodeStyleType 为 null，UI 层应使用默认样式",
                        "WorkflowNode");
                    return null;
                }

                // 使用反射创建样式实例
                var styleInstance = Activator.CreateInstance(_nodeStyleType);
                if (styleInstance != null)
                {
                    VisionLogger.Instance.Log(LogLevel.Success,
                        $"创建节点样式成功: {_nodeStyleType.Name}",
                        "WorkflowNode");
                    return styleInstance;
                }

                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"无法创建节点样式实例: {_nodeStyleType.Name}",
                    "WorkflowNode");
                return null;
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"创建节点样式失败: {ex.Message}",
                    "WorkflowNode", ex);
                return null;
            }
        }

        #endregion

        #region 计算属性（从元数据推断，不序列化）

        /// <summary>
        /// 节点类型（从 ToolRegistry 动态推断，不序列化）
        /// Condition 和 Subroutine 已作为标准 IToolPlugin 由 ToolRegistry 管理，统一为 Algorithm
        /// </summary>
        [JsonIgnore]
        public NodeType NodeType
        {
            get
            {
                // Subroutine 是标准 IToolPlugin，但逻辑上属于 Subroutine 类型（用于 UI 样式区分）
                if (string.Equals(ToolType, "Subroutine", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrEmpty(SubroutineId))
                    return NodeType.Subroutine;

                return NodeType.Algorithm;
            }
        }

        /// <summary>
        /// 显示名称（从控制流注册中心或工具元数据获取，不序列化）
        /// </summary>
        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                var metadata = ToolRegistry.GetToolMetadata(ToolType);
                return metadata?.DisplayName ?? Name;
            }
        }

        /// <summary>
        /// 节点图标（从控制流注册中心或工具元数据获取，不序列化）
        /// </summary>
        [JsonIgnore]
        public string Icon
        {
            get
            {
                var metadata = ToolRegistry.GetToolMetadata(ToolType);
                return metadata?.Icon ?? "?";
            }
        }

        #endregion

        #region 行内重命名

        private bool _isEditingName;
        private string _renameBuffer = string.Empty;

        /// <summary>
        /// 是否正在编辑节点名称
        /// </summary>
        [JsonIgnore]
        public bool IsEditingName
        {
            get => _isEditingName;
            set => SetProperty(ref _isEditingName, value);
        }

        /// <summary>
        /// 编辑中的临时名称缓冲
        /// </summary>
        [JsonIgnore]
        public string RenameBuffer
        {
            get => _renameBuffer;
            set => SetProperty(ref _renameBuffer, value);
        }

        /// <summary>
        /// 开始重命名
        /// </summary>
        public void BeginRename()
        {
            RenameBuffer = DispName;
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
                // 名称为空时不关闭编辑框，让用户继续输入
                return false;
            }

            if (DispName != trimmed)
            {
                DispName = trimmed;
                Name = $"{GlobalIndex} {trimmed}";
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
        /// 执行前事件
        /// </summary>
        public event Action<WorkflowNodeBase> BeforeExecute;

        /// <summary>
        /// 执行后事件
        /// </summary>
        public event Action<WorkflowNodeBase, AlgorithmResult> AfterExecute;

        /// <summary>
        /// 供 System.Text.Json 反序列化使用的无参构造函数
        /// </summary>
        [JsonConstructor]
        public WorkflowNodeBase()
        {
            Id = Guid.NewGuid().ToString();
            Name = string.Empty;
            DispName = string.Empty;
            ToolType = string.Empty;
            Parameters = new GenericToolParameters();
            ParamSettings = new ParamSettingContainer();
        }

        /// <summary>
        /// 创建节点的参数化构造函数
        /// </summary>
        public WorkflowNodeBase(string id, string name, string dispName, string toolType)
        {
            Id = id;
            Name = name;
            DispName = dispName;
            ToolType = toolType;
            Parameters = new GenericToolParameters();
            ParamSettings = new ParamSettingContainer();
        }

        /// <summary>
        /// 触发执行前事件
        /// </summary>
        protected virtual void OnBeforeExecute()
        {
            BeforeExecute?.Invoke(this);
        }

        /// <summary>
        /// 触发执行后事件
        /// </summary>
        protected virtual void OnAfterExecute(AlgorithmResult result)
        {
            AfterExecute?.Invoke(this, result);
        }

        /// <summary>
        /// 添加上游节点（增量更新）
        /// </summary>
        /// <param name="upstreamNodeId">上游节点ID</param>
        /// <param name="outputTypes">上游节点的输出类型列表</param>
        public void AddUpstreamNode(string upstreamNodeId, List<Type> outputTypes)
        {
            if (string.IsNullOrEmpty(upstreamNodeId) || outputTypes == null)
            {
                return;
            }

            foreach (var outputType in outputTypes)
            {
                if (outputType == null)
                {
                    continue;
                }

                if (!UpstreamNodesByOutputType.ContainsKey(outputType))
                {
                    UpstreamNodesByOutputType[outputType] = new List<string>();
                }

                if (!UpstreamNodesByOutputType[outputType].Contains(upstreamNodeId))
                {
                    UpstreamNodesByOutputType[outputType].Add(upstreamNodeId);
                }
            }
        }

        /// <summary>
        /// 移除上游节点（增量更新）
        /// </summary>
        /// <param name="upstreamNodeId">上游节点ID</param>
        /// <param name="outputTypes">上游节点的输出类型列表</param>
        public void RemoveUpstreamNode(string upstreamNodeId, List<Type> outputTypes)
        {
            if (string.IsNullOrEmpty(upstreamNodeId) || outputTypes == null)
            {
                return;
            }

            foreach (var outputType in outputTypes)
            {
                if (outputType == null)
                {
                    continue;
                }

                if (UpstreamNodesByOutputType.ContainsKey(outputType))
                {
                    UpstreamNodesByOutputType[outputType].Remove(upstreamNodeId);

                    // 如果该类型没有上游节点了，移除类型映射
                    if (UpstreamNodesByOutputType[outputType].Count == 0)
                    {
                        UpstreamNodesByOutputType.Remove(outputType);
                    }
                }
            }
        }

        /// <summary>
        /// 清空上游节点池
        /// </summary>
        public void ClearUpstreamNodes()
        {
            UpstreamNodesByOutputType.Clear();
        }

        /// <summary>
        /// 反序列化后回调。子类可重写以初始化默认值。
        /// Phase 2 优化：确保反序列化后的节点初始化 UI 默认值。
        /// </summary>
        public virtual void OnDeserialized()
        {
        }

        /// <summary>
        /// 创建工具实例（的初始化处理方法）
        /// </summary>
        public virtual IToolPlugin? CreateInstance()
        {
            // 从工具注册表创建工具实例
            var instance = ToolRegistry.CreateToolInstance(ToolType);
            
            if (instance == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning, 
                    $"无法创建工具实例: ToolType={ToolType}", 
                    "WorkflowNodeBase");
            }
            
            return instance;
        }

        /// <summary>
        /// 从字典创建节点（仅用于兼容旧格式 JSON 的反序列化）
        /// </summary>
        public static WorkflowNodeBase FromDictionary(Dictionary<string, object> dict)
        {
            var id = dict.TryGetValue("Id", out var idVal) ? idVal?.ToString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
            var name = dict.TryGetValue("Name", out var nameVal) ? nameVal?.ToString() ?? "Node" : "Node";
            var dispName = dict.TryGetValue("DispName", out var dispNameVal) ? dispNameVal?.ToString() ?? string.Empty : string.Empty;
            var toolType = dict.TryGetValue("ToolType", out var toolTypeVal) ? toolTypeVal?.ToString() ?? string.Empty : string.Empty;
            var isEnabled = dict.TryGetValue("IsEnabled", out var enabledVal) && Convert.ToBoolean(enabledVal);

            // 读取位置属性
            var positionX = dict.TryGetValue("PositionX", out var xVal) ? Convert.ToDouble(xVal) : 0.0;
            var positionY = dict.TryGetValue("PositionY", out var yVal) ? Convert.ToDouble(yVal) : 0.0;
            var width = dict.TryGetValue("Width", out var wVal) ? Convert.ToDouble(wVal) : 140.0;
            var height = dict.TryGetValue("Height", out var hVal) ? Convert.ToDouble(hVal) : 90.0;

            // 读取索引属性
            var localIndex = dict.TryGetValue("LocalIndex", out var localIdx) ? Convert.ToInt32(localIdx) : -1;
            var globalIndex = dict.TryGetValue("GlobalIndex", out var globalIdx) ? Convert.ToInt32(globalIdx) : -1;

            // 推断节点类型（优先读取 NodeType 字段，否则从 ToolType 推断）
            NodeType nodeType = NodeType.Algorithm;
            if (dict.TryGetValue("NodeType", out var typeVal))
            {
                if (typeVal is int intVal && Enum.IsDefined(typeof(NodeType), intVal))
                {
                    nodeType = (NodeType)intVal;
                }
                else if (typeVal is string strVal && Enum.TryParse<NodeType>(strVal, out var parsed))
                {
                    nodeType = parsed;
                }
            }

            var node = new WorkflowNodeBase(id, name, dispName, toolType)
            {
                LocalIndex = localIndex,
                GlobalIndex = globalIndex,
                IsEnabled = isEnabled,
                PositionX = positionX,
                PositionY = positionY,
                Width = width,
                Height = height
            };

            // 恢复参数
            if (dict.TryGetValue("Parameters", out var paramsVal) && paramsVal != null)
            {
                // 参数对象已经由 JsonSerializer 直接反序列化
                // 无需使用 Dictionary 转换层
                if (paramsVal is ToolParameters parameters)
                {
                    node.Parameters = parameters;
                    // 🔍 详细日志：节点参数恢复成功
                    var paramSummary = parameters.GetParameterSummary();
                    VisionLogger.Instance.Log(LogLevel.Success,
                        $"✅ [节点参数恢复] 节点: {name} | 类型: {parameters.GetType().Name} | 值: {paramSummary}",
                        "WorkflowNode");
                }
                else
                {
                    // ✅ 如果参数为 null 或类型不匹配，直接抛出异常
                    throw new InvalidOperationException(
                        $"节点 {name} 参数恢复失败：期望 ToolParameters，实际为 {paramsVal?.GetType().Name ?? "null"}");
                }
            }
            else
            {
                // ✅ 参数为空时直接抛出异常
                throw new InvalidOperationException($"节点 {name} 参数为空");
            }

            // 恢复参数绑定
            if (dict.TryGetValue("ParamSettings", out var bindingsVal) && bindingsVal is Dictionary<string, object> bindingsDict)
            {
                node.ParamSettings = ParamSettingContainer.FromDictionary(bindingsDict);
            }

            return node;
        }

        /// <summary>
        /// 克隆节点
        /// </summary>
        public WorkflowNodeBase Clone()
        {
            var cloned = new WorkflowNodeBase(
                Id,
                $"{Name}_副本",
                DispName,
                ToolType
            )
            {
                IsEnabled = IsEnabled,
            AdditionalData = AdditionalData?.ToDictionary(p => p.Key, p => p.Value.Clone()),
                Parameters = Parameters,
                ParamSettings = ParamSettings?.Clone(),
                PositionX = PositionX,
                PositionY = PositionY,
                Width = Width,
                Height = Height,
                SelectedDisplaySource = SelectedDisplaySource
            };

            return cloned;
        }
    }
}
