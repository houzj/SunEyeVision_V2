using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Common.Localization;

public class ResourceBinding : Binding
{
	private class LangConverter : IValueConverter
	{
		public static readonly LangConverter Current = new LangConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value == null)
			{
				return value;
			}
			string text = value.ToString();
			string text2 = AppLocalizationService.GetInternalLang(text);
			if (text2 == text)
			{
				text2 = AppLocalizationService.GetLang(text);
			}
			return text2;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public ResourceBinding()
	{
		Converter = LangConverter.Current;
	}

	public ResourceBinding(string path)
		: this()
	{
		Path = new PropertyPath(path);
	}
}
