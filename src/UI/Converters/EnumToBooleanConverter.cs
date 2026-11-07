using System;
using System.Globalization;
using System.Windows.Data;
using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.UI.Converters
{
    /// <summary>
    /// 枚举值转布尔值转换器
    /// </summary>
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is Enum enumValue && parameter is Enum enumParameter)
                {
                    var result = enumValue.Equals(enumParameter);
                    return result;
                }
                return false;
            }
            catch (Exception ex)
            {
                PluginLogger.Error($"[EnumToBooleanConverter] Convert 发生异常: {ex.Message}\n{ex.StackTrace}", "UI");
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is bool boolValue && parameter is Enum enumParameter)
                {
                    return boolValue ? enumParameter : Binding.DoNothing;
                }
                return Binding.DoNothing;
            }
            catch (Exception ex)
            {
                PluginLogger.Error($"[EnumToBooleanConverter] ConvertBack 发生异常: {ex.Message}\n{ex.StackTrace}", "UI");
                return Binding.DoNothing;
            }
        }
    }
}
