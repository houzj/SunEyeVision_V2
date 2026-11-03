using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core.Utilities;

public class ContextMenuUtilities
{
	public static readonly DependencyProperty OpenOnMouseLeftButtonClickProperty = DependencyProperty.RegisterAttached("OpenOnMouseLeftButtonClick", typeof(bool), typeof(ContextMenuUtilities), new FrameworkPropertyMetadata(false, OpenOnMouseLeftButtonClickChanged));

	public static void SetOpenOnMouseLeftButtonClick(FrameworkElement element, bool value)
	{
		element.SetValue(OpenOnMouseLeftButtonClickProperty, value);
	}

	public static bool GetOpenOnMouseLeftButtonClick(FrameworkElement element)
	{
		return (bool)element.GetValue(OpenOnMouseLeftButtonClickProperty);
	}

	public static void OpenOnMouseLeftButtonClickChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
	{
		FrameworkElement control = (FrameworkElement)sender;
		if (!(bool)e.NewValue)
		{
			return;
		}
		control.PreviewMouseLeftButtonDown += (object s, MouseButtonEventArgs args) =>
		{
			if (control.ContextMenu != null)
			{
				control.ContextMenu.PlacementTarget = control;
				control.ContextMenu.IsOpen = true;
			}
		};
	}
}
