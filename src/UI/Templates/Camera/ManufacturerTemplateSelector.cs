using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.UI.Features.Camera
{
    /// <summary>
    /// 厂商模板选择器
    /// </summary>
    /// <remarks>
    /// 根据厂商类型选择不同的参数编辑模板
    /// </remarks>
    public class ManufacturerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate GenericTemplate { get; set; }
        public DataTemplate HikvisionTemplate { get; set; }
        public DataTemplate DahuaTemplate { get; set; }
        public DataTemplate BaslerTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            // TODO: 未来实现厂商特定参数时，取消注释以下代码
            // if (item is HikvisionParamsViewModel)
            //     return HikvisionTemplate ?? GenericTemplate;
            // if (item is DahuaParamsViewModel)
            //     return DahuaTemplate ?? GenericTemplate;
            // if (item is BaslerParamsViewModel)
            //     return BaslerTemplate ?? GenericTemplate;

            // 当前所有相机类型都使用通用模板
            return GenericTemplate;
        }
    }
}
