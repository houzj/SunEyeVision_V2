using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Tool.BlobAnalysis.Converters
{
    /// <summary>
    /// 阈值类型转换器 - 支持 ComboBox 与 ThresholdType 枚举的双向绑定
    /// </summary>
    public class ThresholdTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ThresholdType thresholdType)
            {
                return thresholdType.ToString();
            }
            return "Binary";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string typeStr)
            {
                return typeStr switch
                {
                    "Binary" => ThresholdType.Binary,
                    "BinaryInv" => ThresholdType.BinaryInv,
                    _ => ThresholdType.Binary
                };
            }
            return ThresholdType.Binary;
        }
    }

    /// <summary>
    /// 连通性类型转换器 - 支持 ComboBox 与 ConnectivityType 枚举的双向绑定
    /// </summary>
    public class ConnectivityTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConnectivityType connectivityType)
            {
                return connectivityType switch
                {
                    ConnectivityType.Connect4 => "4连通",
                    ConnectivityType.Connect8 => "8连通",
                    _ => "8连通"
                };
            }
            return "8连通";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string typeStr)
            {
                return typeStr switch
                {
                    "4连通" => ConnectivityType.Connect4,
                    "8连通" => ConnectivityType.Connect8,
                    _ => ConnectivityType.Connect8
                };
            }
            return ConnectivityType.Connect8;
        }
    }
}
