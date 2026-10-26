using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

public static class TreeHelper
{
	public static T TryFindParent<T>(this DependencyObject child) where T : DependencyObject
	{
		DependencyObject parentObject = child.GetParentObject();
		if (parentObject == null)
		{
			return null;
		}
		if (parentObject is T result)
		{
			return result;
		}
		return parentObject.TryFindParent<T>();
	}

	public static DependencyObject GetParentObject(this DependencyObject child)
	{
		if (child == null)
		{
			return null;
		}
		if (child is ContentElement contentElement)
		{
			DependencyObject parent = ContentOperations.GetParent(contentElement);
			if (parent != null)
			{
				return parent;
			}
			return (contentElement is FrameworkContentElement frameworkContentElement) ? frameworkContentElement.Parent : null;
		}
		if (child is FrameworkElement { Parent: { } parent2 })
		{
			return parent2;
		}
		return VisualTreeHelper.GetParent(child);
	}

	public static T FindPointProperty<T>(UIElement reference, Point point) where T : DependencyObject
	{
		if (!(reference.InputHitTest(point) is DependencyObject dependencyObject))
		{
			return null;
		}
		if (!(dependencyObject is T result))
		{
			return dependencyObject.TryFindParent<T>();
		}
		return result;
	}

	public static object GetDataContext(ItemsControl itemsControl, Point p)
	{
		object result = null;
		if (itemsControl.InputHitTest(p) is FrameworkElement frameworkElement)
		{
			result = frameworkElement.DataContext;
		}
		return result;
	}
}
