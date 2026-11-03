using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// AI分析节点显示适配器
    /// </summary>
    public class AIAnalysisNodeDisplayAdapter : INodeDisplayAdapter
    {
        public string GetDisplayText(CanvasNodeProjection node)
        {
            return node.DispName;
        }

        public string GetIcon(CanvasNodeProjection node)
        {
            return "🧠";
        }

        public Color GetBackgroundColor(CanvasNodeProjection node)
        {
            return Color.FromRgb(240, 230, 255); // 淡紫色背景
        }

        public Color GetBorderColor(CanvasNodeProjection node)
        {
            return Color.FromRgb(138, 43, 226); // 蓝紫色
        }
    }
}
