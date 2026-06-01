using System.Collections.ObjectModel;

namespace SunEyeVision.UI.Models
{
    /// <summary>
    /// 导航帧 - 保存一个层级（主工作流或某级子程序）的完整画布状态
    /// </summary>
    public class NavigationFrame
    {
        /// <summary>显示名称（用于Tab Header）</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>对应的工作流ID</summary>
        public string WorkflowId { get; set; } = string.Empty;

        /// <summary>调用此层的节点ID（用于返回时高亮）</summary>
        public string? CallerNodeId { get; set; }

        /// <summary>节点集合引用</summary>
        public ObservableCollection<WorkflowNode>? Nodes { get; set; }

        /// <summary>连接线集合引用</summary>
        public ObservableCollection<WorkflowConnection>? Connections { get; set; }

        /// <summary>当前缩放比例</summary>
        public double CurrentScale { get; set; } = 1.0;
    }
}
