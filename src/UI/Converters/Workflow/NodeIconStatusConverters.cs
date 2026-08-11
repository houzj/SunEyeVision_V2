using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.UI.Converters.Workflow
{
    /// <summary>
    /// 画布节点图标块背景转换（方案 §6 三态）：
    /// "OK" → 实色绿；"NG" → 实色红；其他（待运行/未执行）→ 透明（保持现状，不影响其他节点）。
    /// </summary>
    public class NodeIconStatusBackgroundConverter : IValueConverter
    {
        private static readonly Brush GreenBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));    // #4CAF50
        private static readonly Brush RedBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54));      // #F44336
        private static readonly Brush TransparentBrush = Brushes.Transparent;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                "OK" => GreenBrush,
                "NG" => RedBrush,
                _ => TransparentBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("NodeIconStatusBackgroundConverter 不支持反向转换。");
        }
    }

    /// <summary>
    /// 画布节点图标前景色转换（方案 §6 三态）：
    /// "OK"/"NG" → 白色（在实色背景上可读）；其他（待运行/未执行）→ 深色（保持现状可见）。
    /// </summary>
    public class NodeIconStatusForegroundConverter : IValueConverter
    {
        private static readonly Brush WhiteBrush = Brushes.White;
        private static readonly Brush DarkBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51)); // #333333

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                "OK" => WhiteBrush,
                "NG" => WhiteBrush,
                _ => DarkBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("NodeIconStatusForegroundConverter 不支持反向转换。");
        }
    }
}
