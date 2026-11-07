using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using Apps.Localization;

namespace ShellModuleControl;

public class DisplayConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value != null)
		{
			ObservableCollection<object> observableCollection = value as ObservableCollection<object>;
			if (observableCollection.Count == 0)
			{
				observableCollection.Add(AppLocalizationService.GetLang("Lang_RstEpt"));
			}
			return observableCollection;
		}
		return value;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
