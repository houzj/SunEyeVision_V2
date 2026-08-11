using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Adapters;

namespace SunEyeVision.UI.Adapters
{
    /// <summary>
    /// 逻辑节点显示适配器
    /// </summary>
    public class LogicNodeDisplayAdapter : ICategoryDisplayAdapter
    {
        /// <summary>
        /// 支持的分类
        /// </summary>
        public string[] SupportedCategories => new[] { "逻辑" };

        public string GetDisplayText(WorkflowNode node)
        {
            return $"{node.DisplayName}_{node.LocalIndex}";
        }

        public string GetIcon(WorkflowNode node)
        {
            return "⚡";
        }

        public Color GetBackgroundColor(WorkflowNode node)
        {
            return Color.FromRgb(224, 247, 250); // #E0F7FA
        }

        public Color GetBorderColor(WorkflowNode node)
        {
            return Color.FromRgb(0, 151, 167); // #0097A7
        }
    }
}
