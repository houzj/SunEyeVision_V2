using SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.ParameterPresentation.ParamTabControl;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTabControl;

public class IndexConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		dynamic val = values[0];
		dynamic val2 = values[1];
		try
		{
			return val2.IndexOf(val) + 1;
		}
		catch (Exception)
		{
			return "";
		}
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
