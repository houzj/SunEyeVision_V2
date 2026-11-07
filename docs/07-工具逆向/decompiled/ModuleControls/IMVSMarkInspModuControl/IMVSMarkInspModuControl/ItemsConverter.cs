using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;

namespace IMVSMarkInspModuControl;

public class ItemsConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return true;
		}
		ObservableCollection<ModelImage> observableCollection = value as ObservableCollection<ModelImage>;
		if (observableCollection.Count == 0)
		{
			return true;
		}
		return false;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
