using System;
using System.Windows;
using System.Windows.Controls;

namespace DynamicObjectConfigControl;

public static class TabControlHelper
{
	public static readonly DependencyProperty AutoScrollToSelectedItemProperty = DependencyProperty.RegisterAttached("AutoScrollToSelectedItem", typeof(bool), typeof(TabControlHelper), new PropertyMetadata(false, (DependencyObject d, DependencyPropertyChangedEventArgs e) =>
	{
		TabControl tabControl = d as TabControl;
		bool flag = default;
		int num;
		if (tabControl != null)
		{
			object newValue = e.NewValue;
			if (newValue is bool)
			{
				flag = (bool)newValue;
				num = 1;
			}
			else
			{
				num = 0;
			}
		}
		else
		{
			num = 0;
		}
		if (((uint)num & (flag ? 1u : 0u)) != 0)
		{
			tabControl.SelectionChanged += (object s, SelectionChangedEventArgs args) =>
			{
				if (tabControl.SelectedItem != null)
				{
					tabControl.Dispatcher.BeginInvoke((Action)(() =>
					{
						tabControl.UpdateLayout();
						if (tabControl.ItemContainerGenerator.ContainerFromItem(tabControl.SelectedItem) is FrameworkElement frameworkElement)
						{
							frameworkElement.BringIntoView();
						}
					}));
				}
			};
		}
	}));

	public static bool GetAutoScrollToSelectedItem(DependencyObject obj)
	{
		return (bool)obj.GetValue(AutoScrollToSelectedItemProperty);
	}

	public static void SetAutoScrollToSelectedItem(DependencyObject obj, bool value)
	{
		obj.SetValue(AutoScrollToSelectedItemProperty, value);
	}
}
