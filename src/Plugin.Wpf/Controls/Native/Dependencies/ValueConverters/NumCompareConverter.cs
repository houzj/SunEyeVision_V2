using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.ValueConverters;

// Apps.BaseData 1.0.0.0, MVID b537fea2-7c48-4764-95c5-67bf1b261b29.
// Targeted static decompilation; preserve the original value-type and null behavior.
public class NumCompareConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)
    {
        if(parameter.GetType().IsValueType&&value.GetType().IsValueType)
        {
            double num=System.Convert.ToDouble(value);
            double num2=System.Convert.ToDouble(parameter);
            return num>num2?1:num<num2?-1:0;
        }
        return 0;
    }
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)
        =>throw new NotImplementedException();
}
