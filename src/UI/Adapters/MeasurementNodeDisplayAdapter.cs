using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Adapters;

namespace SunEyeVision.UI.Adapters
{
    /// <summary>
    /// 测量节点显示适配器
    /// </summary>
    public class MeasurementNodeDisplayAdapter : ICategoryDisplayAdapter
    {
        /// <summary>
        /// 支持的分类
        /// </summary>
        public string[] SupportedCategories => new[] { "测量" };

        public string GetDisplayText(WorkflowNode node)
        {
            return $"{node.DisplayName}_{node.LocalIndex}";
        }

        public string GetIcon(WorkflowNode node)
        {
            return "📐";
        }

        public Color GetBackgroundColor(WorkflowNode node)
        {
            return Color.FromRgb(255, 249, 196); // #FFF9C4
        }

        public Color GetBorderColor(WorkflowNode node)
        {
            return Color.FromRgb(251, 192, 45); // #FBC02D
        }
    }
}
