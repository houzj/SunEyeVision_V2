using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Tool.Condition.Views
{
    /// <summary>
    /// 条件规则类型转换器：RuleType（"Int"/"Double"/"String"）→ CLR Type。
    /// 用于 ParamBinding.DataType 按规则类型过滤可绑定的上游数据源。
    /// Int/Double 统一映射 double（范围判断都转数值），String 映射 string。
    /// 未映射类型直接抛异常（rule-015，禁止静默回退）。
    /// </summary>
    public class ConditionTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                "Int" => typeof(double),
                "Double" => typeof(double),
                "String" => typeof(string),
                _ => throw new NotSupportedException($"不支持的条件规则类型: {value ?? "(null)"}。可用类型: Int / Double / String。")
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("ConditionTypeConverter 不支持反向转换。");
        }
    }
}
