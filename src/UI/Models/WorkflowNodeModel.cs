using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.UI.Models
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
    public class WorkflowNode : WorkflowNodeBase
    {
        /// <summary>
        /// 节点输出缓存（UI 层专用，强类型）
        /// </summary>
        public new NodeOutputCache? OutputCache
        {
            get => base.OutputCache as NodeOutputCache;
            set => base.OutputCache = value;
        }

        /// <summary>
        /// 节点输入源（UI 层专用，强类型）
        /// </summary>
        public new ImageInputSource? InputSource
        {
            get => base.InputSource as ImageInputSource;
            set => base.InputSource = value;
        }

        /// <summary>
        /// 最近一次执行结果（UI 层专用，强类型）
        /// </summary>
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
            get => base.StyleConfig as NodeStyleConfig ?? NodeStyles.Standard;
            set => base.StyleConfig = value;
        }

        /// <summary>
        /// 节点扩展数据（工具/插件自定义的附加数据，不序列化）
        /// </summary>
        [JsonIgnore]
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
        }

        /// <summary>
        /// 供 System.Text.Json 反序列化使用的无参构造函数
        /// </summary>
        [System.Text.Json.Serialization.JsonConstructor]
        public WorkflowNode() : base()
        {
            StyleConfigTyped = NodeStyles.Standard;
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
                GlobalIndex = baseNode.GlobalIndex,
                LocalIndex = baseNode.LocalIndex,
                PositionX = baseNode.PositionX,
                PositionY = baseNode.PositionY,
                Width = baseNode.Width,
                Height = baseNode.Height,
                IsEnabled = baseNode.IsEnabled,
                Parameters = baseNode.Parameters,
                SelectedDisplaySource = baseNode.SelectedDisplaySource
            };

            return node;
        }

    #endregion
    }
}
