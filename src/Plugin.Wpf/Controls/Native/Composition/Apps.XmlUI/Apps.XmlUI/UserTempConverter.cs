using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class UserTempConverter : IValueConverter
{
	public static ResourceDictionary _ResourceDictionary;

	public static List<Assembly> ExternAssembly = new List<Assembly>();

	public ResourceDictionary ResourceDictionary
	{
		get
		{
			if (_ResourceDictionary == null)
			{
				Uri resourceLocator = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Composition/Apps.XmlUI/Styles/ParamUserControlDataTemplate.xaml", UriKind.Relative);
				_ResourceDictionary = (ResourceDictionary)Application.LoadComponent(resourceLocator);
			}
			return _ResourceDictionary;
		}
	}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (ResourceDictionary == null)
		{
			return null;
		}
		return (!ResourceDictionary.Contains((string)value)) ? (ResourceDictionary["Default"] as DataTemplate) : (ResourceDictionary[(string)value] as DataTemplate);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
