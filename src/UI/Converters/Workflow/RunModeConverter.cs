using System;
using System.Globalization;
using System.Windows.Data;
using SunEyeVision.UI.Shared;

using SunEyeVision.UI.Features.WorkflowUI;

namespace SunEyeVision.UI.Converters.Workflow
{
    /// <summary>
    /// 运行模式到字符串的转换器
    /// </summary>
    public class RunModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RunMode mode)
            {
                return mode switch
                {
                    RunMode.Single => "单次",
                    RunMode.Continuous => "连续",
                    _ => "单次"
                };
            }
            return "单次";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "单次" => RunMode.Single,
                    "连续" => RunMode.Continuous,
                    _ => RunMode.Single
                };
            }
            return RunMode.Single;
        }
    }
}
