using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.UI.Converters
{
    /// <summary>
    /// 类型到颜色转换器
    /// </summary>
    public class TypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return new SolidColorBrush(Colors.LightGray);

            string? type = value.ToString();
            return type switch
            {
                "String" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),   // 蓝色
                "Int32" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),    // 绿色
                "Double" => new SolidColorBrush(Color.FromRgb(156, 39, 176)), // 紫色
                "Boolean" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),  // 橙色
                "DateTime" => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // 红色
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))          // 灰色
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值到可见性转换器
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool invert = parameter?.ToString() == "Invert";
                return (boolValue ^ invert) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Visibility visibility)
            {
                bool invert = parameter?.ToString() == "Invert";
                return (visibility == System.Windows.Visibility.Visible) ^ invert;
            }
            return false;
        }
    }

    /// <summary>
    /// 通讯类型到图标转换器
    /// </summary>
    public class CommunicationTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CommunicationType type)
            {
                return type switch
                {
                    CommunicationType.HTTP => "🌐",
                    CommunicationType.ModbusTCP => "🔌",
                    CommunicationType.ModbusRTU => "📡",
                    CommunicationType.OPCUA => "⚙️",
                    CommunicationType.TCP => "🔗",
                    CommunicationType.UDP => "📤",
                    CommunicationType.Serial => "🔌",
                    _ => "📡"
                };
            }
            return "📡";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 设备状态到图标转换器
    /// </summary>
    public class DeviceStatusToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DeviceStatus status)
            {
                return status switch
                {
                    DeviceStatus.Unknown => "❓",
                    DeviceStatus.Connected => "✅",
                    DeviceStatus.Disconnected => "❌",
                    DeviceStatus.Error => "⚠️",
                    DeviceStatus.Initializing => "⏳",
                    _ => "❓"
                };
            }
            return "❓";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 设备状态到颜色转换器
    /// </summary>
    public class DeviceStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DeviceStatus status)
            {
                return status switch
                {
                    DeviceStatus.Unknown => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                    DeviceStatus.Connected => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    DeviceStatus.Disconnected => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                    DeviceStatus.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    DeviceStatus.Initializing => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
                };
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 连接字符串转换器
    /// </summary>
    public class ConnectionStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Communication communication)
            {
                return communication.GetConnectionString();
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 整数转可见性转换器
    /// 非0值返回 Visible，0值返回 Collapsed
    /// </summary>
    public class IntToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 值为 0 时返回的可见性，默认为 Collapsed
        /// </summary>
        public System.Windows.Visibility ZeroValueVisibility { get; set; } = System.Windows.Visibility.Collapsed;

        /// <summary>
        /// 值非 0 时返回的可见性，默认为 Visible
        /// </summary>
        public System.Windows.Visibility NonZeroValueVisibility { get; set; } = System.Windows.Visibility.Visible;

        /// <summary>
        /// 是否反转逻辑（值非 0 时隐藏，值为 0 时显示）
        /// </summary>
        public bool Invert { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return Invert ? NonZeroValueVisibility : ZeroValueVisibility;
            }

            if (value is int intValue)
            {
                bool isVisible = intValue != 0;
                if (Invert)
                {
                    isVisible = !isVisible;
                }
                return isVisible ? NonZeroValueVisibility : ZeroValueVisibility;
            }

            return ZeroValueVisibility;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Visibility visibility)
            {
                bool isVisible = visibility == System.Windows.Visibility.Visible;
                if (Invert)
                {
                    isVisible = !isVisible;
                }
                return isVisible ? 1 : 0;
            }

            return 0;
        }
    }
}
