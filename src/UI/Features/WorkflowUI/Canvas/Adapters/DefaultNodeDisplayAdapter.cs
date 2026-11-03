using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 默认节点显示适配器。
    /// </summary>
    public class DefaultNodeDisplayAdapter : INodeDisplayAdapter
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
            return Colors.White;
        }

        public Color GetBorderColor(CanvasNodeProjection node)
        {
            return Color.FromRgb(255, 149, 0); // #ff9500
        }
    }
}
