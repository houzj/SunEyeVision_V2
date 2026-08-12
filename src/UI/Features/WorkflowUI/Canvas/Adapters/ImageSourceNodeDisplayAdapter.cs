using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 图像源节点显示适配器
    /// </summary>
    public class ImageSourceNodeDisplayAdapter : ICategoryDisplayAdapter
    {
        /// <summary>
        /// 支持的分类
        /// </summary>
        public string[] SupportedCategories => new[] { "采集", "输入" };

        public string GetDisplayText(WorkflowNode node)
        {
            return $"{node.DisplayName}_{node.LocalIndex}";
        }

        public string GetIcon(WorkflowNode node)
        {
            return "📷";
        }

        public Color GetBackgroundColor(WorkflowNode node)
        {
            return Color.FromRgb(240, 248, 255); // 淡蓝色背景
        }

        public Color GetBorderColor(WorkflowNode node)
        {
            return Color.FromRgb(65, 105, 225); // 皇家蓝
        }
    }
}
