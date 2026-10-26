using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.UI.Shared
{
    /// <summary>
    /// 工作流节点模型 - UI 层扩展属性
    /// </summary>
    /// <remarks>
    /// 重构说明（2026-03-26）：
    /// - 继承 WorkflowNodeBase，不再重复实现 INotifyPropertyChanged
    /// - WorkflowNodeBase 已包含 Position, IsSelected, IsVisible, Status 等属性
    /// - 本类仅添加 UI 层特有的强类型属性和计算属性
    ///
    /// 数据源统一：
    /// - UI 直接绑定 Solution.Workflow.Nodes（ObservableCollection）
    /// - 不再需要同步代码
    /// </remarks>
    public partial class WorkflowNode : WorkflowNodeBase
    {
        /// <summary>
        /// 节点输出缓存（UI 层专用，强类型）
        /// 基类属性已 [JsonIgnore]，此处 new 隐藏属性必须显式 [JsonIgnore]，
        /// 否则按运行时类型序列化 WorkflowNode 时会把强类型对象带进 JSON。
        /// </summary>
        [JsonIgnore]
        public new NodeOutputCache? OutputCache
        {
            get => base.OutputCache as NodeOutputCache;
            set => base.OutputCache = value;
        }

        /// <summary>
        /// 节点输入源（UI 层专用，强类型）
        /// 基类属性已 [JsonIgnore]，此处 new 隐藏属性必须显式 [JsonIgnore]，
        /// 否则 ImageInputSource（含 BitmapImage）会被序列化进 JSON。
        /// </summary>
        [JsonIgnore]
        public new ImageInputSource? InputSource
        {
            get => base.InputSource as ImageInputSource;
            set
            {
                // 解除旧输入源的回调，避免切换节点后残留旧引用
                if (base.InputSource is ImageInputSource oldSource)
                {
                    oldSource.ConfigChanged = null;
                }

                base.InputSource = value;

                // ★ 内存实时同步：InputSource 任何变更（加/删/清/切图、模式）立即写回 ImageSourceConfig，
                // 与全局变量/参数实时同步模式对齐；保存时序列化 ImageSourceConfig 即得最新状态。
                if (value != null)
                {
                    value.ConfigChanged = cfg =>
                    {
                        ImageSourceConfig ??= new ImageSourceConfig { NodeId = Id };
                        ImageSourceConfig.UpdateLocalInput(cfg);
                    };
                }
            }
        }

        /// <summary>
        /// 最近一次执行结果（UI 层专用，强类型）
        /// 基类属性已 [JsonIgnore]，此处 new 隐藏属性必须显式 [JsonIgnore]。
        /// </summary>
        [JsonIgnore]
        public new ToolResults? LastResult
        {
            get => base.LastResult as ToolResults;
            set => base.LastResult = value;
        }

        /// <summary>
        /// 节点样式配置（UI 层专用，强类型）
        /// </summary>
        public NodeStyleConfig StyleConfigTyped
        {
            get => IsSubroutineNode ? NodeStyles.Group : base.StyleConfig as NodeStyleConfig ?? NodeStyles.Standard;
            set => base.StyleConfig = value;
        }

        /// <summary>
        /// 节点扩展数据（工具/插件自定义的附加数据）
        /// Phase 3: 移除 [JsonIgnore] 使其参与序列化，确保子程序关联保存/加载不丢失
        /// </summary>
        [JsonInclude]
        public Dictionary<string, object>? ExtraData { get; set; }

        #region 端口位置计算属性

        /// <summary>
        /// 获取上方连接点位置（动态计算）
        /// </summary>
        public Point TopPortPosition => StyleConfigTyped.GetTopPortPosition(Position);

        /// <summary>
        /// 获取下方连接点位置（动态计算）
        /// </summary>
        public Point BottomPortPosition => StyleConfigTyped.GetBottomPortPosition(Position);

        /// <summary>
        /// 获取左侧连接点位置（动态计算）
        /// </summary>
        public Point LeftPortPosition => StyleConfigTyped.GetLeftPortPosition(Position);

        /// <summary>
        /// 获取右侧连接点位置（动态计算）
        /// </summary>
        public Point RightPortPosition => StyleConfigTyped.GetRightPortPosition(Position);

        /// <summary>
        /// 获取节点边界矩形（用于框选等操作）
        /// </summary>
        [JsonIgnore] public bool IsSubroutineNode => string.Equals(ToolType, "Subroutine", StringComparison.OrdinalIgnoreCase);
        [JsonIgnore] public double VisualWidth => StyleConfigTyped.NodeWidth;
        [JsonIgnore] public double VisualHeight => StyleConfigTyped.NodeHeight;
        [JsonIgnore] public double HitAreaWidth => VisualWidth + 20;
        [JsonIgnore] public double HitAreaHeight => VisualHeight + 20;
        [JsonIgnore] public double HitAreaLeft => Position.X - HitAreaWidth / 2;
        [JsonIgnore] public double HitAreaTop => Position.Y - HitAreaHeight / 2;
        public Rect NodeRect => StyleConfigTyped.GetNodeRect(Position);

        /// <summary>
        /// 获取节点中心点（用于距离计算）
        /// </summary>
        public Point NodeCenter => StyleConfigTyped.GetNodeCenter(Position);

        #endregion

        #region 类型判断属性

        /// <summary>
        /// 是否为图像采集节点（基于 ToolType 计算）
        /// </summary>
        public bool IsImageCaptureNode => ToolType != null && (
            ToolType.Contains("ImageCapture", StringComparison.OrdinalIgnoreCase) ||
            ToolType.Contains("Camera", StringComparison.OrdinalIgnoreCase) ||
            ToolType.Contains("视频源", StringComparison.OrdinalIgnoreCase) ||
            ToolType.Contains("VideoSource", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 是否为图像加载节点（基于 ToolType 计算）
        /// </summary>
        public bool IsImageLoadNode => ToolType != null && (
            ToolType.Contains("ImageLoad", StringComparison.OrdinalIgnoreCase) ||
            ToolType.Contains("图片载入", StringComparison.OrdinalIgnoreCase) ||
            ToolType.Contains("ImageSource", StringComparison.OrdinalIgnoreCase));

        #endregion

        /// <summary>
        /// 确保输入源已初始化
        /// </summary>
        public ImageInputSource EnsureInputSource()
        {
            if (InputSource == null)
            {
                InputSource = new ImageInputSource(Id);
                OnPropertyChanged(nameof(InputSource));
            }
            return InputSource;
        }

        #region 构造函数

        public WorkflowNode(string id, string name, string dispName, string toolType)
            : base(id, name, dispName, toolType)
        {
            // 初始化样式配置
            StyleConfigTyped = NodeStyles.Standard;
            InitializeVmPresentation();
        }

        /// <summary>
        /// 供 System.Text.Json 反序列化使用的无参构造函数
        /// </summary>
        [System.Text.Json.Serialization.JsonConstructor]
        public WorkflowNode() : base()
        {
            StyleConfigTyped = NodeStyles.Standard;
            InitializeVmPresentation();
        }

        #endregion

        #region 类型转换

        /// <summary>
        /// 从 WorkflowNodeBase 创建 UI 层 WorkflowNode（数据层 → UI 层的统一转换入口）
        /// </summary>
        /// <remarks>
        /// 如果 baseNode 本身就是 WorkflowNode，直接返回；
        /// 否则创建新的 WorkflowNode 并完整复制所有属性。
        /// 确保加载解决方案时不丢失位置、参数、尺寸等信息。
        /// </remarks>
        public static WorkflowNode FromBase(WorkflowNodeBase baseNode)
        {
            if (baseNode is WorkflowNode existing)
                return existing;

            var node = new WorkflowNode(
                baseNode.Id,
                baseNode.Name,
                baseNode.DispName ?? baseNode.Name,
                baseNode.ToolType)
            {
                AdditionalData = baseNode.AdditionalData?.ToDictionary(p => p.Key, p => p.Value.Clone()),
                GlobalIndex = baseNode.GlobalIndex,
                LocalIndex = baseNode.LocalIndex,
                PositionX = baseNode.PositionX,
                PositionY = baseNode.PositionY,
                Width = baseNode.Width,
                Height = baseNode.Height,
                IsEnabled = baseNode.IsEnabled,
                Parameters = baseNode.Parameters,
                // 绑定定义属于设计期节点状态，必须随基础节点转换到 UI 节点；
                // 遗漏此字段会导致方案文件中已有 ParamSettings 在重启加载时丢失。
                ParamSettings = baseNode.ParamSettings,
                SelectedDisplaySource = baseNode.SelectedDisplaySource,
                SubroutineId = baseNode.SubroutineId,    // ★ SSOT
                ImageSourceConfig = baseNode.ImageSourceConfig   // ★ 输入源持久化
            };

            // 若反序列化了输入源配置，则据此重建运行时 InputSource（ImageInputSource）
            if (node.ImageSourceConfig != null && node.ImageSourceConfig.FilePaths.Count > 0)
            {
                var input = node.EnsureInputSource();
                input.LoadFromConfig(node.ImageSourceConfig);
                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[FromBase] 已从Config恢复图像源 | Node={node.Name} | 恢复后Count={input.Count}",
                    "WorkflowNodeModel");
            }

            return node;
        }

    #endregion
    }
}
