using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 图像源选择变更事件参数
    /// </summary>
    public class ImageSourceSelectionChangedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// 选中的数据源
        /// </summary>
        public AvailableDataSource SelectedSource { get; }

        public ImageSourceSelectionChangedEventArgs(AvailableDataSource selectedSource)
        {
            SelectedSource = selectedSource;
        }

        public ImageSourceSelectionChangedEventArgs(AvailableDataSource selectedSource, RoutedEvent routedEvent)
            : base(routedEvent)
        {
            SelectedSource = selectedSource;
        }
    }
}
