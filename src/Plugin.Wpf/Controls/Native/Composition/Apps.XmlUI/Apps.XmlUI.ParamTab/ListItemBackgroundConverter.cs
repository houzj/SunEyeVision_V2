using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

public sealed class ListItemBackgroundConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		ListViewItem container = (ListViewItem)value;
		ListView listView = ItemsControl.ItemsControlFromItemContainer(container) as ListView;
		int num = listView.ItemContainerGenerator.IndexFromContainer(container);
		if (num % 2 == 0)
		{
			return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBEBEB"));
		}
		return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
