using System.Windows;
using System.Windows.Controls;
using SunEyeVision.UI.ViewModels;

namespace SunEyeVision.UI.Views.Controls.Canvas
{
    /// <summary>
    /// 画布模板选择器
    /// </summary>
    public class CanvasTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// WorkflowCanvasControl 的模板
        /// </summary>
        public DataTemplate? WorkflowCanvasTemplate { get; set; }

        /// <summary>
        /// 根据画布类型选择模板
        /// </summary>
        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            // 统一返回 WorkflowCanvasTemplate
            return WorkflowCanvasTemplate;
        }
    }
}
