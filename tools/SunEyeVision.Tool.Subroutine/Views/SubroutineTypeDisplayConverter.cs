using System;
using System.Globalization;
using System.Windows.Data;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.Subroutine.Views
{
    /// <summary>
    /// 子程序类型中文显示转换器：
    /// string（TypeName 词表名，如 "Image"）→ 中文（"图像"）；
    /// SubroutineDisplayType（枚举，如 Circle）→ 中文（"圆"）。
    /// 未映射时显示原文（词表 GetChineseName 的既有语义），不静默成空。
    /// </summary>
    public class SubroutineTypeDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                string typeName => SdkTypeVocabulary.GetChineseName(typeName) ?? typeName,
                SubroutineDisplayType displayType => displayType.GetChineseName(),
                null => string.Empty,
                _ => value.ToString() ?? string.Empty
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("SubroutineTypeDisplayConverter 不支持反向转换。");
        }
    }
}
