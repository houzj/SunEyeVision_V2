using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Tool.ImageFilter.Converters
{
    /// <summary>
    /// 滤波类型转换器 - 支持 ComboBox 与 FilterType 枚举的双向绑定
    /// </summary>
    public class FilterTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FilterType filterType)
            {
                return filterType switch
                {
                    FilterType.Blur => "均值滤波",
                    FilterType.Gaussian => "高斯滤波",
                    FilterType.MedianBlur => "中值滤波",
                    FilterType.Bilateral => "双边滤波",
                    FilterType.Filter2D => "自定义卷积",
                    FilterType.HistogramEqualization => "直方图均衡化",
                    _ => "高斯滤波"
                };
            }
            return "高斯滤波";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string typeStr)
            {
                return typeStr switch
                {
                    "均值滤波" => FilterType.Blur,
                    "高斯滤波" => FilterType.Gaussian,
                    "中值滤波" => FilterType.MedianBlur,
                    "双边滤波" => FilterType.Bilateral,
                    "自定义卷积" => FilterType.Filter2D,
                    "直方图均衡化" => FilterType.HistogramEqualization,
                    _ => FilterType.Gaussian
                };
            }
            return FilterType.Gaussian;
        }
    }
}
