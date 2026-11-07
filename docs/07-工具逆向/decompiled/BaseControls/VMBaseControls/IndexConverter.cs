using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace VMBaseControls;

public class IndexConverter : IValueConverter
{
	public object Convert(object value, Type TargetType, object parameter, CultureInfo culture)
	{
		int result = 0;
		int.TryParse(parameter.ToString(), out result);
		ListViewItem container = (ListViewItem)value;
		ListView listView = ItemsControl.ItemsControlFromItemContainer(container) as ListView;
		int num = listView.ItemContainerGenerator.IndexFromContainer(container);
		return num + result;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
