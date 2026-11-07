using System.Windows.Media;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 定位节点显示适配器
    /// </summary>
    public class LocationNodeDisplayAdapter : ICategoryDisplayAdapter
    {
        /// <summary>
        /// 支持的分类
        /// </summary>
        public string[] SupportedCategories => new[] { SunEyeVision.Plugin.Editing.Wpf.Core.ToolCategories.Location };

        public string GetDisplayText(CanvasNodeProjection node)
        {
            return node.DispName;
        }

        public string GetIcon(CanvasNodeProjection node)
        {
            return "🎯";
        }

        public Color GetBackgroundColor(CanvasNodeProjection node)
        {
            return Color.FromRgb(255, 243, 224); // #FFF3E0
        }

        public Color GetBorderColor(CanvasNodeProjection node)
        {
            return Color.FromRgb(245, 124, 0); // #F57C00
        }
    }
}
