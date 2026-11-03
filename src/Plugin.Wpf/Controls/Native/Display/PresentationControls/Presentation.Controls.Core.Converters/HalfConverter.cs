using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core.Converters;

public class HalfConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		double num = (double)value;
		double num2 = ((parameter != null) ? double.Parse((string)parameter) : 0.0);
		if (num2 != 0.0)
		{
			return Math.Max(0.0, num - num2) / 2.0;
		}
		return num / 2.0;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
