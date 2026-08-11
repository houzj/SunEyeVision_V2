using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Adapters;

namespace SunEyeVision.UI.Adapters
{
    /// <summary>
    /// AI分析节点显示适配器
    /// </summary>
    public class AIAnalysisNodeDisplayAdapter : INodeDisplayAdapter
    {
        public string GetDisplayText(WorkflowNode node)
        {
            return $"{node.DisplayName}_{node.LocalIndex}";
        }

        public string GetIcon(WorkflowNode node)
        {
            return "🧠";
        }

        public Color GetBackgroundColor(WorkflowNode node)
        {
            return Color.FromRgb(240, 230, 255); // 淡紫色背景
        }

        public Color GetBorderColor(WorkflowNode node)
        {
            return Color.FromRgb(138, 43, 226); // 蓝紫色
        }
    }
}
