using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class ItemCheckConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is NativeParameterItemState itemState)
		{
			return itemState switch
			{
				NativeParameterItemState.ReadError => (object)Brushes.Yellow, 
				NativeParameterItemState.WriteError => Brushes.Red, 
				_ => Brushes.Transparent, 
			};
		}
		return Brushes.Transparent;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
