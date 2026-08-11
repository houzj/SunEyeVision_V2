using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Adapters;

namespace SunEyeVision.UI.Adapters
{
    /// <summary>
    /// 定位节点显示适配器
    /// </summary>
    public class LocationNodeDisplayAdapter : ICategoryDisplayAdapter
    {
        /// <summary>
        /// 支持的分类
        /// </summary>
        public string[] SupportedCategories => new[] { "定位" };

        public string GetDisplayText(WorkflowNode node)
        {
            return $"{node.DisplayName}_{node.LocalIndex}";
        }

        public string GetIcon(WorkflowNode node)
        {
            return "🎯";
        }

        public Color GetBackgroundColor(WorkflowNode node)
        {
            return Color.FromRgb(255, 243, 224); // #FFF3E0
        }

        public Color GetBorderColor(WorkflowNode node)
        {
            return Color.FromRgb(245, 124, 0); // #F57C00
        }
    }
}
