using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Logging;

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
        private static string? _lastBackgroundLogKey;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value as string;
            Brush result = status switch
            {
                "运行中" => RunningBrush,
                "成功" or "OK" => GreenBrush,
                "失败" or "NG" => RedBrush,
                "跳过" => GrayBrush,
                _ => TransparentBrush
            };

            // 节流日志：确认状态色块背景（待运行是否透明，暗色下会露出下层背景）
            var logKey = $"{status ?? "(null)"}→{(result as SolidColorBrush)?.Color.ToString() ?? "Transparent"}";
            if (_lastBackgroundLogKey != logKey)
            {
                _lastBackgroundLogKey = logKey;
                VisionLogger.Instance?.Log(LogLevel.Info,
                    $"节点状态色块背景解析: {logKey}",
                    "NodeIconStatusBackgroundConverter");
            }
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("NodeIconStatusBackgroundConverter 不支持反向转换。");
        }
    }

    /// <summary>
    /// 画布节点图标前景色转换（方案 §6 五态）：
    /// 运行中/成功/失败/跳过/OK/NG → 白色（在实色背景上可读）；其他（待运行）→ 写死深灰 #333333。
    /// 治本：原 ResolveThemeTextBrush 读 ThemeKeys.TextSecondaryBrush，
    /// 亮色主题下=#666666 节点浅色底看 ✓；暗色主题下=#C0C0C0 与节点卡片背景(Adapter 写死浅色)撞色失败。
    /// 用户决策（2026-08-17）：节点视觉表达全冻结亮色主题这一套，亮暗都用同一组文字色，不再跟主题走。
    /// </summary>
    public class NodeIconStatusForegroundConverter : IValueConverter
    {
        private static readonly Brush WhiteBrush = Brushes.White;
        private static readonly Brush FrozenPendingBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51)); // #333333 冻结：待运行图标永远深灰
        private static string? _lastForegroundLogKey;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value as string;
            Brush result = status switch
            {
                "运行中" => WhiteBrush,
                "成功" or "OK" => WhiteBrush,
                "失败" or "NG" => WhiteBrush,
                "跳过" => WhiteBrush,
                _ => FrozenPendingBrush
            };

            // 节流日志：确认节点图标前景色（待运行=写死 #333333，节点视觉冻结 2026-08-17）
            var colorDesc = result is SolidColorBrush scb ? scb.Color.ToString() : "非纯色";
            var logKey = $"{status ?? "(null)"}→{colorDesc}";
            if (_lastForegroundLogKey != logKey)
            {
                _lastForegroundLogKey = logKey;
                VisionLogger.Instance?.Log(LogLevel.Info,
                    $"节点图标前景色解析: {logKey}",
                    "NodeIconStatusForegroundConverter");
            }
            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("NodeIconStatusForegroundConverter 不支持反向转换。");
        }

        // ResolveThemeTextBrush 已删除：节点视觉冻结亮色主题（用户决策 2026-08-17），
        // 待运行图标前景一律用 FrozenPendingBrush(#333333)，不再读主题资源。
    }
}
