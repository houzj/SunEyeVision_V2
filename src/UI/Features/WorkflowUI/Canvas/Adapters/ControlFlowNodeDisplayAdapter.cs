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
        public string GetDisplayText(CanvasNodeProjection node)
        {
            return node.DispName;
        }

        public string GetIcon(CanvasNodeProjection node)
        {
            return node.Catalog?.IconKey ?? string.Empty;
        }

        public Color GetBackgroundColor(CanvasNodeProjection node)
        {
            // 所有流程控制节点统一使用淡靛蓝背景色
            return Color.FromRgb(232, 234, 246); // #E8EAF6
        }

        public Color GetBorderColor(CanvasNodeProjection node)
        {
            // 所有流程控制节点统一使用靛蓝边框色
            return Color.FromRgb(92, 107, 192); // #5C6BC0
        }
    }
}
