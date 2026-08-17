using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Converters.Node
{
    /// <summary>
    /// 节点显示文本转换器 - 使用适配器获取显示文本
    /// </summary>
    public class NodeDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkflowNode node)
            {
                var adapter = NodeDisplayAdapterFactory.GetAdapter(node.ToolType);
                return adapter.GetDisplayText(node);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 节点背景颜色转换器 - 使用适配器获取背景颜色
    /// </summary>
    public class NodeBackgroundColorConverter : IValueConverter
    {
        private static string? _lastBackgroundLogKey;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkflowNode node)
            {
                var adapter = NodeDisplayAdapterFactory.GetAdapter(node.ToolType);
                var color = adapter.GetBackgroundColor(node);
                // 节流日志：确认节点背景色（暗色主题下是否仍为浅色，判定图标对比度来源）
                var logKey = $"{node.ToolType}→{color}";
                if (_lastBackgroundLogKey != logKey)
                {
                    _lastBackgroundLogKey = logKey;
                    Plugin.SDK.Logging.VisionLogger.Instance?.Log(
                        Plugin.SDK.Logging.LogLevel.Info,
                        $"节点背景色解析: {logKey}",
                        "NodeBackgroundColorConverter");
                }
                return new SolidColorBrush(color);
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 节点边框颜色转换器 - 使用适配器获取边框颜色
    /// </summary>
    public class NodeBorderColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WorkflowNode node)
            {
                var adapter = NodeDisplayAdapterFactory.GetAdapter(node.ToolType);
                return new SolidColorBrush(adapter.GetBorderColor(node));
            }
            return new SolidColorBrush(Color.FromRgb(255, 149, 0)); // #ff9500
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 颜色到画刷转换器（通用）
    /// </summary>
    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return new SolidColorBrush(color);
            }
            if (value is string colorString && TryParseColor(colorString, out Color parsedColor))
            {
                return new SolidColorBrush(parsedColor);
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private bool TryParseColor(string colorString, out Color color)
        {
            if (ColorConverter.ConvertFromString(colorString) is Color parsedColor)
            {
                color = parsedColor;
                return true;
            }
            color = Colors.White;
            return false;
        }
    }
}
