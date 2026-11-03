using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 多集合节点显示适配器。
    /// </summary>
    public class MultiCollectionNodeDisplayAdapter : INodeDisplayAdapter
    {
        public string GetDisplayText(CanvasNodeProjection node)
        {
            return node.DispName;
        }

        public string GetIcon(CanvasNodeProjection node)
        {
            return "🔄";
        }

        public Color GetBackgroundColor(CanvasNodeProjection node)
        {
            return Color.FromRgb(245, 245, 245); // 淡灰色背景。
        }

        public Color GetBorderColor(CanvasNodeProjection node)
        {
            return Color.FromRgb(128, 128, 128); // 灰色
        }
    }
}
