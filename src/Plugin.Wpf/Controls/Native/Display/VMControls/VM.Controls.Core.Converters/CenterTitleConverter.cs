using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Converters;

public class CenterTitleConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		double width = ((Size)values[0]).Width;
		double num = (double)values[1];
		ColumnDefinitionCollection columnDefinitionCollection = (ColumnDefinitionCollection)values[2];
		double actualWidth = columnDefinitionCollection[2].ActualWidth;
		double actualWidth2 = columnDefinitionCollection[3].ActualWidth;
		if (width + actualWidth2 * 2.0 < num)
		{
			return 1;
		}
		if (width < actualWidth)
		{
			return 2;
		}
		return 3;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
