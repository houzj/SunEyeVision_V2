using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

public static class VisualTreeHelperEx
{
	public static DependencyObject FindAncestorByType(DependencyObject element, Type type, bool specificTypeOnly)
	{
		if (element == null)
		{
			return null;
		}
		bool num;
		if (!specificTypeOnly)
		{
			if (element.GetType() == type)
			{
				goto IL_0035;
			}
			num = element.GetType().IsSubclassOf(type);
		}
		else
		{
			num = element.GetType() == type;
		}
		if (!num)
		{
			return FindAncestorByType(VisualTreeHelper.GetParent(element), type, specificTypeOnly);
		}
		goto IL_0035;
		IL_0035:
		return element;
	}

	public static T FindAncestorByType<T>(DependencyObject depObj) where T : DependencyObject
	{
		if (depObj == null)
		{
			return null;
		}
		if (depObj is T)
		{
			return (T)depObj;
		}
		return FindAncestorByType<T>(VisualTreeHelper.GetParent(depObj));
	}

	public static Visual FindDescendantByName(Visual element, string name)
	{
		if (element != null && element is FrameworkElement && (element as FrameworkElement).Name == name)
		{
			return element;
		}
		Visual visual = null;
		if (element is FrameworkElement)
		{
			(element as FrameworkElement).ApplyTemplate();
		}
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
		{
			visual = FindDescendantByName(VisualTreeHelper.GetChild(element, i) as Visual, name);
			if (visual != null)
			{
				break;
			}
		}
		return visual;
	}

	public static Visual FindDescendantByType(Visual element, Type type)
	{
		return FindDescendantByType(element, type, specificTypeOnly: true);
	}

	public static Visual FindDescendantByType(Visual element, Type type, bool specificTypeOnly)
	{
		if (element == null)
		{
			return null;
		}
		bool num;
		if (!specificTypeOnly)
		{
			if (element.GetType() == type)
			{
				goto IL_0035;
			}
			num = element.GetType().IsSubclassOf(type);
		}
		else
		{
			num = element.GetType() == type;
		}
		if (!num)
		{
			Visual visual = null;
			if (element is FrameworkElement)
			{
				(element as FrameworkElement).ApplyTemplate();
			}
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
			{
				visual = FindDescendantByType(VisualTreeHelper.GetChild(element, i) as Visual, type, specificTypeOnly);
				if (visual != null)
				{
					break;
				}
			}
			return visual;
		}
		goto IL_0035;
		IL_0035:
		return element;
	}

	public static T FindDescendantByType<T>(Visual element) where T : Visual
	{
		return (T)FindDescendantByType(element, typeof(T));
	}

	public static Visual FindDescendantWithPropertyValue(Visual element, DependencyProperty dp, object value)
	{
		if (element == null)
		{
			return null;
		}
		if (element.GetValue(dp).Equals(value))
		{
			return element;
		}
		Visual visual = null;
		if (element is FrameworkElement)
		{
			(element as FrameworkElement).ApplyTemplate();
		}
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
		{
			visual = FindDescendantWithPropertyValue(VisualTreeHelper.GetChild(element, i) as Visual, dp, value);
			if (visual != null)
			{
				break;
			}
		}
		return visual;
	}

	public static childItem FindVisualChild<childItem>(this DependencyObject obj, string controlName) where childItem : DependencyObject
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(obj, i);
			if (child != null && child is childItem)
			{
				if (child is FrameworkElement frameworkElement && frameworkElement.Name == controlName)
				{
					return (childItem)child;
				}
				continue;
			}
			childItem val = child.FindVisualChild<childItem>(controlName);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}
}
