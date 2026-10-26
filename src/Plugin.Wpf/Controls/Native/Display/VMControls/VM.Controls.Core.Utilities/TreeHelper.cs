using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal static class TreeHelper
{
	public static DependencyObject GetParent(DependencyObject element)
	{
		return GetParent(element, recurseIntoPopup: true);
	}

	private static DependencyObject GetParent(DependencyObject element, bool recurseIntoPopup)
	{
		if (recurseIntoPopup && element is Popup { PlacementTarget: not null } popup)
		{
			return popup.PlacementTarget;
		}
		DependencyObject dependencyObject = ((!(element is Visual reference)) ? null : VisualTreeHelper.GetParent(reference));
		if (dependencyObject == null)
		{
			if (element is FrameworkElement frameworkElement)
			{
				dependencyObject = frameworkElement.Parent;
				if (dependencyObject == null)
				{
					dependencyObject = frameworkElement.TemplatedParent;
				}
			}
			else if (element is FrameworkContentElement frameworkContentElement)
			{
				dependencyObject = frameworkContentElement.Parent;
				if (dependencyObject == null)
				{
					dependencyObject = frameworkContentElement.TemplatedParent;
				}
			}
		}
		return dependencyObject;
	}

	public static T FindParent<T>(DependencyObject startingObject) where T : DependencyObject
	{
		return FindParent<T>(startingObject, checkStartingObject: false, null);
	}

	public static T FindParent<T>(DependencyObject startingObject, bool checkStartingObject) where T : DependencyObject
	{
		return FindParent<T>(startingObject, checkStartingObject, null);
	}

	public static T FindParent<T>(DependencyObject startingObject, bool checkStartingObject, Func<T, bool> additionalCheck) where T : DependencyObject
	{
		for (DependencyObject dependencyObject = (checkStartingObject ? startingObject : GetParent(startingObject, recurseIntoPopup: true)); dependencyObject != null; dependencyObject = GetParent(dependencyObject, recurseIntoPopup: true))
		{
			if (dependencyObject is T val)
			{
				if (additionalCheck == null)
				{
					return val;
				}
				if (additionalCheck(val))
				{
					return val;
				}
			}
		}
		return null;
	}

	public static T FindChild<T>(DependencyObject parent) where T : DependencyObject
	{
		return FindChild<T>(parent, null);
	}

	public static T FindChild<T>(DependencyObject parent, Func<T, bool> additionalCheck) where T : DependencyObject
	{
		int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
		for (int i = 0; i < childrenCount; i++)
		{
			if (VisualTreeHelper.GetChild(parent, i) is T val)
			{
				if (additionalCheck == null)
				{
					return val;
				}
				if (additionalCheck(val))
				{
					return val;
				}
			}
		}
		for (int j = 0; j < childrenCount; j++)
		{
			T val2 = FindChild(VisualTreeHelper.GetChild(parent, j), additionalCheck);
			if (val2 != null)
			{
				return val2;
			}
		}
		return null;
	}

	public static bool IsDescendantOf(DependencyObject element, DependencyObject parent)
	{
		return IsDescendantOf(element, parent, recurseIntoPopup: true);
	}

	public static bool IsDescendantOf(DependencyObject element, DependencyObject parent, bool recurseIntoPopup)
	{
		while (element != null)
		{
			if (element == parent)
			{
				return true;
			}
			element = GetParent(element, recurseIntoPopup);
		}
		return false;
	}
}
