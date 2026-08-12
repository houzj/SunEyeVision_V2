using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 控制流节点显示适配器（子程序、条件判断等）
    /// </summary>
    public class ControlFlowNodeDisplayAdapter : INodeDisplayAdapter
    {
        public string GetDisplayText(WorkflowNode node)
        {
            return $"{node.DisplayName}_{node.LocalIndex}";
        }

        public string GetIcon(WorkflowNode node)
        {
            return node.Icon ?? "?";
        }

        public Color GetBackgroundColor(WorkflowNode node)
        {
            // 所有流程控制节点统一使用淡靛蓝背景色
            return Color.FromRgb(232, 234, 246); // #E8EAF6
        }

        public Color GetBorderColor(WorkflowNode node)
        {
            // 所有流程控制节点统一使用靛蓝边框色
            return Color.FromRgb(92, 107, 192); // #5C6BC0
        }
    }
}
