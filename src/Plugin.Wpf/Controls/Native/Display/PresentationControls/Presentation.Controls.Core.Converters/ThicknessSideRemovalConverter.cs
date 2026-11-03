using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core.Converters;

public class ThicknessSideRemovalConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		Thickness thickness = (Thickness)value;
		switch (int.Parse((string)parameter))
		{
		case 0:
			thickness.Left = 0.0;
			break;
		case 1:
			thickness.Top = 0.0;
			break;
		case 2:
			thickness.Right = 0.0;
			break;
		case 3:
			thickness.Bottom = 0.0;
			break;
		default:
			throw new InvalidContentException("parameter should be from 0 to 3 to specify the side to remove.");
		}
		return thickness;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
