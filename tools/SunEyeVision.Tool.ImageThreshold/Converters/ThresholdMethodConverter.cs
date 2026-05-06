using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Tool.ImageThreshold.Converters
{
    /// <summary>
    /// 阈值方法转换器 - 支持 ComboBox 与 ThresholdMethod 枚举的双向绑定
    /// 枚举值 <-> 中文显示文本
    /// </summary>
    public class ThresholdMethodConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ThresholdMethod method)
            {
                return method switch
                {
                    ThresholdMethod.Binary => "固定阈值",
                    ThresholdMethod.BinaryInv => "反二值化",
                    ThresholdMethod.Trunc => "截断",
                    ThresholdMethod.ToZero => "取零",
                    ThresholdMethod.ToZeroInv => "反取零",
                    ThresholdMethod.Otsu => "大津法(Otsu)",
                    ThresholdMethod.Triangle => "三角形法(Triangle)",
                    ThresholdMethod.AdaptiveMean => "自适应均值",
                    ThresholdMethod.AdaptiveGaussian => "自适应高斯",
                    ThresholdMethod.InRange => "范围阈值",
                    _ => "固定阈值"
                };
            }
            return "固定阈值";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return text switch
                {
                    "固定阈值" => ThresholdMethod.Binary,
                    "反二值化" => ThresholdMethod.BinaryInv,
                    "截断" => ThresholdMethod.Trunc,
                    "取零" => ThresholdMethod.ToZero,
                    "反取零" => ThresholdMethod.ToZeroInv,
                    "大津法(Otsu)" => ThresholdMethod.Otsu,
                    "三角形法(Triangle)" => ThresholdMethod.Triangle,
                    "自适应均值" => ThresholdMethod.AdaptiveMean,
                    "自适应高斯" => ThresholdMethod.AdaptiveGaussian,
                    "范围阈值" => ThresholdMethod.InRange,
                    _ => ThresholdMethod.Binary
                };
            }
            return ThresholdMethod.Binary;
        }
    }
}
