using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Tool.Threshold.Converters
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
                    "Trunc" => ThresholdType.Trunc,
                    "ToZero" => ThresholdType.ToZero,
                    "ToZeroInv" => ThresholdType.ToZeroInv,
                    _ => ThresholdType.Binary
                };
            }
            return ThresholdType.Binary;
        }
    }
}
