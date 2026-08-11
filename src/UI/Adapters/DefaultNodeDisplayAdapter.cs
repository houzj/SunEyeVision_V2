using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Adapters;

namespace SunEyeVision.UI.Adapters
{
    /// <summary>
    /// 默认节点显示适配器。
    /// </summary>
    public class DefaultNodeDisplayAdapter : INodeDisplayAdapter
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
            return Colors.White;
        }

        public Color GetBorderColor(WorkflowNode node)
        {
            return Color.FromRgb(255, 149, 0); // #ff9500
        }
    }
}
