using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Tool.ImageFilter.Converters
{
    /// <summary>
    /// 边界类型转换器 - 支持 ComboBox 与 BorderType 枚举的双向绑定
    /// </summary>
    public class BorderTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BorderType borderType)
            {
                return borderType switch
                {
                    BorderType.Reflect => "镜像反射(Reflect)",
                    BorderType.Replicate => "边缘复制(Replicate)",
                    BorderType.Constant => "常量填充(Constant)",
                    BorderType.Reflect101 => "镜像反射101(Reflect101)",
                    BorderType.Wrap => "环绕(Wrap)",
                    BorderType.Isolated => "独立(Isolated)",
                    _ => "镜像反射(Reflect)"
                };
            }
            return "镜像反射(Reflect)";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string typeStr)
            {
                return typeStr switch
                {
                    "镜像反射(Reflect)" => BorderType.Reflect,
                    "边缘复制(Replicate)" => BorderType.Replicate,
                    "常量填充(Constant)" => BorderType.Constant,
                    "镜像反射101(Reflect101)" => BorderType.Reflect101,
                    "环绕(Wrap)" => BorderType.Wrap,
                    "独立(Isolated)" => BorderType.Isolated,
                    _ => BorderType.Reflect
                };
            }
            return BorderType.Reflect;
        }
    }
}
