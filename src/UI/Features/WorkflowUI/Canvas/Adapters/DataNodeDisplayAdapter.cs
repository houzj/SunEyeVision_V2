using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 数据节点显示适配器
    /// </summary>
    public class DataNodeDisplayAdapter : ICategoryDisplayAdapter
    {
        /// <summary>
        /// 支持的分类
        /// </summary>
        public string[] SupportedCategories => new[] { "数据" };

        public string GetDisplayText(WorkflowNode node)
        {
            return $"{node.DisplayName}_{node.LocalIndex}";
        }

        public string GetIcon(WorkflowNode node)
        {
            return "📊";
        }

        public Color GetBackgroundColor(WorkflowNode node)
        {
            return Color.FromRgb(245, 245, 245); // #F5F5F5
        }

        public Color GetBorderColor(WorkflowNode node)
        {
            return Color.FromRgb(97, 97, 97); // #616161
        }
    }
}
