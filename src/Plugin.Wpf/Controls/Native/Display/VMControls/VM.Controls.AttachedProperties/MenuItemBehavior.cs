using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.AttachedProperties;

public static class MenuItemBehavior
{
	public static readonly DependencyProperty RoutedProperty = DependencyProperty.RegisterAttached("Routed", typeof(bool), typeof(MenuItemBehavior), new PropertyMetadata(false, PropertyChangedCallback));

	private static void PropertyChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
	{
		if (dependencyObject is MenuItem menuItem && VisualTreeHelper.GetParent(menuItem) is GroupItem)
		{
			if (menuItem.IsLoaded)
			{
				AddHandlers(menuItem);
			}
			else
			{
				menuItem.Loaded += MiOnLoaded;
			}
		}
	}

	private static void MiOnLoaded(object sender, RoutedEventArgs routedEventArgs)
	{
		MenuItem menuItem = (MenuItem)sender;
		menuItem.Loaded -= MiOnLoaded;
		AddHandlers(menuItem);
	}

	private static void AddHandlers(MenuItem mi)
	{
		mi.MouseEnter += MiOnMouseEnter;
		mi.MouseLeave += MiOnMouseLeave;
	}

	private static void MiOnMouseLeave(object sender, MouseEventArgs mouseEventArgs)
	{
		MenuItem menuItem = (MenuItem)sender;
		if (menuItem.IsSubmenuOpen)
		{
			menuItem.IsSubmenuOpen = false;
		}
	}

	private static void MiOnMouseEnter(object sender, MouseEventArgs mouseEventArgs)
	{
		if (sender is MenuItem { Items: not null } menuItem && menuItem.Items.Count != 0)
		{
			menuItem.IsSubmenuOpen = true;
		}
	}

	public static void SetRouted(MenuItem menuItem, bool value)
	{
		menuItem.SetValue(RoutedProperty, value);
	}

	public static bool GetRouted(MenuItem menuItem)
	{
		object value = menuItem.GetValue(RoutedProperty);
		if (value != null)
		{
			return (bool)value;
		}
		return false;
	}
}
