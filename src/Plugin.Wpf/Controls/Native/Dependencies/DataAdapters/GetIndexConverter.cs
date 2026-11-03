using System;
using System.Globalization;
using System.Windows.Data;
namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.DataAdapters;
/// <summary>Original one-based display index; never a geometry identity.</summary>
public sealed class GetIndexConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is null?0:unchecked(System.Convert.ToInt32(value)+1);
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotImplementedException();
}
