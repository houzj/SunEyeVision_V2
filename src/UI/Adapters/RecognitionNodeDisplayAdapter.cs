using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Adapters;

namespace SunEyeVision.UI.Adapters
{
    /// <summary>
    /// 识别节点显示适配器
    /// </summary>
    public class RecognitionNodeDisplayAdapter : ICategoryDisplayAdapter
    {
        /// <summary>
        /// 支持的分类
        /// </summary>
        public string[] SupportedCategories => new[] { "识别" };

        public string GetDisplayText(WorkflowNode node)
        {
            return $"{node.DisplayName}_{node.LocalIndex}";
        }

        public string GetIcon(WorkflowNode node)
        {
            return "🔍";
        }

        public Color GetBackgroundColor(WorkflowNode node)
        {
            return Color.FromRgb(243, 229, 245); // #F3E5F5
        }

        public Color GetBorderColor(WorkflowNode node)
        {
            return Color.FromRgb(123, 31, 162); // #7B1FA2
        }
    }
}
