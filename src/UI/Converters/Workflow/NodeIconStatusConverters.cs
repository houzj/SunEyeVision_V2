using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.UI.Converters.Workflow
{
    /// <summary>
    /// 画布节点图标块背景转换（方案 §6 五态）：
    /// "运行中" → 蓝；"成功"/"OK" → 绿；"失败"/"NG" → 红；"跳过" → 灰；其他（待运行）→ 透明。
    /// </summary>
    public class NodeIconStatusBackgroundConverter : IValueConverter
    {
        private static readonly Brush RunningBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243));    // #2196F3 蓝
        private static readonly Brush GreenBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));      // #4CAF50 绿
        private static readonly Brush RedBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54));        // #F44336 红
        private static readonly Brush GrayBrush = new SolidColorBrush(Color.FromRgb(158, 158, 158));     // #9E9E9E 灰
        private static readonly Brush TransparentBrush = Brushes.Transparent;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                "运行中" => RunningBrush,
                "成功" or "OK" => GreenBrush,
                "失败" or "NG" => RedBrush,
                "跳过" => GrayBrush,
                _ => TransparentBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("NodeIconStatusBackgroundConverter 不支持反向转换。");
        }
    }

    /// <summary>
    /// 画布节点图标前景色转换（方案 §6 五态）：
    /// 运行中/成功/失败/跳过/OK/NG → 白色（在实色背景上可读）；其他（待运行）→ 深色（保持现状可见）。
    /// </summary>
    public class NodeIconStatusForegroundConverter : IValueConverter
    {
        private static readonly Brush WhiteBrush = Brushes.White;
        private static readonly Brush DarkBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51)); // #333333

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                "运行中" => WhiteBrush,
                "成功" or "OK" => WhiteBrush,
                "失败" or "NG" => WhiteBrush,
                "跳过" => WhiteBrush,
                _ => DarkBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("NodeIconStatusForegroundConverter 不支持反向转换。");
        }
    }
}
