using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal static class RoutedEventHelper
{
	internal static void RaiseEvent(DependencyObject target, RoutedEventArgs args)
	{
		if (target is UIElement)
		{
			(target as UIElement).RaiseEvent(args);
		}
		else if (target is ContentElement)
		{
			(target as ContentElement).RaiseEvent(args);
		}
	}

	internal static void AddHandler(DependencyObject element, RoutedEvent routedEvent, Delegate handler)
	{
		if (element is UIElement uIElement)
		{
			uIElement.AddHandler(routedEvent, handler);
		}
		else if (element is ContentElement contentElement)
		{
			contentElement.AddHandler(routedEvent, handler);
		}
	}

	internal static void RemoveHandler(DependencyObject element, RoutedEvent routedEvent, Delegate handler)
	{
		if (element is UIElement uIElement)
		{
			uIElement.RemoveHandler(routedEvent, handler);
		}
		else if (element is ContentElement contentElement)
		{
			contentElement.RemoveHandler(routedEvent, handler);
		}
	}
}
