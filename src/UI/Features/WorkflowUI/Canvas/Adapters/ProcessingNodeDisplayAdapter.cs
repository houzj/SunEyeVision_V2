using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 处理节点显示适配器
    /// </summary>
    public class ProcessingNodeDisplayAdapter : ICategoryDisplayAdapter
    {
        /// <summary>
        /// 支持的分类
        /// </summary>
        public string[] SupportedCategories => new[] { "图像处理" };

        public string GetDisplayText(WorkflowNode node)
        {
            return $"{node.DisplayName}_{node.LocalIndex}";
        }

        public string GetIcon(WorkflowNode node)
        {
            return "⚙️";
        }

        public Color GetBackgroundColor(WorkflowNode node)
        {
            return Color.FromRgb(240, 255, 240); // 淡绿色背景
        }

        public Color GetBorderColor(WorkflowNode node)
        {
            return Color.FromRgb(34, 139, 34); // 森林绿
        }
    }
}
