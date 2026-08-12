using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 视频源节点显示适配器
    /// </summary>
    public class VideoSourceNodeDisplayAdapter : INodeDisplayAdapter
    {
        public string GetDisplayText(WorkflowNode node)
        {
            return $"{node.DisplayName}_{node.LocalIndex}";
        }

        public string GetIcon(WorkflowNode node)
        {
            return "📹";
        }

        public Color GetBackgroundColor(WorkflowNode node)
        {
            return Color.FromRgb(255, 240, 240); // 淡红色背景
        }

        public Color GetBorderColor(WorkflowNode node)
        {
            return Color.FromRgb(220, 20, 60); // 猩红
        }
    }
}
