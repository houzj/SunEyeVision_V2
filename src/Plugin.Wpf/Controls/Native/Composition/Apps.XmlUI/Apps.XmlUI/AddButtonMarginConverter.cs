using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class AddButtonMarginConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value != null)
		{
			ObservableCollection<object> observableCollection = value as ObservableCollection<object>;
			if (observableCollection.Count <= 1)
			{
				return new Thickness(29.0, 1.0, 70.0, 0.0);
			}
			return new Thickness(0.0, 5.0, 0.0, 0.0);
		}
		return new Thickness(0.0, 5.0, 0.0, 0.0);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
