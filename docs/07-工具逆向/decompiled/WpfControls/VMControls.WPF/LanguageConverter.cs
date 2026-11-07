using System;
using System.Globalization;
using System.Windows.Data;
using Apps.Localization;

namespace VMControls.WPF;

public class LanguageConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return AppLocalizationService.GetLang(value.ToString());
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
