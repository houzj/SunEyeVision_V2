using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.AttachedProperties;

public static class GroupBehavior
{
	public static readonly DependencyProperty FilterProperty = DependencyProperty.RegisterAttached("Filter", typeof(string), typeof(GroupBehavior), new PropertyMetadata(null, PropertyChangedCallback));

	public static readonly DependencyProperty FilterSourceProperty = DependencyProperty.RegisterAttached("FilterSource", typeof(IEnumerable), typeof(GroupBehavior), new PropertyMetadata(null, PropertyChangedCallback));

	public static void SetFilter(ItemsControl itemsControl, string value)
	{
		itemsControl.SetValue(FilterProperty, value);
	}

	public static string GetFilter(ItemsControl itemsControl)
	{
		return (string)itemsControl.GetValue(FilterProperty);
	}

	private static void PropertyChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
	{
		if (!(dependencyObject is ItemsControl { ItemsSource: CollectionView itemsSource } itemsControl))
		{
			return;
		}
		string[] groupname = (from p in itemsSource.GroupDescriptions.OfType<PropertyGroupDescription>()
			select p.PropertyName).ToArray();
		if (!groupname.Any())
		{
			return;
		}
		ICollectionView defaultView = CollectionViewSource.GetDefaultView(itemsSource.SourceCollection);
		string[] array = groupname;
		foreach (string propertyName in array)
		{
			defaultView.GroupDescriptions.Add(new PropertyGroupDescription
			{
				PropertyName = propertyName
			});
		}
		string filter = GetFilter(itemsControl);
		filter = filter ?? string.Empty;
		string[] filter2 = filter.Split(',');
		defaultView.Filter = (object item) =>
		{
			string[] array2 = groupname;
			foreach (string name in array2)
			{
				PropertyInfo property = item.GetType().GetProperty(name);
				if (!(property == null))
				{
					object value = property.GetValue(item);
					if (value != null && filter2.Contains(value.ToString()))
					{
						return false;
					}
				}
			}
			return true;
		};
		itemsControl.ItemsSource = defaultView;
	}

	public static void SetFilterSource(ItemsControl itemsControl, IEnumerable value)
	{
		itemsControl.SetValue(FilterSourceProperty, value);
	}

	public static IEnumerable GetFilterSource(ItemsControl itemsControl)
	{
		return (IEnumerable)itemsControl.GetValue(FilterSourceProperty);
	}
}
