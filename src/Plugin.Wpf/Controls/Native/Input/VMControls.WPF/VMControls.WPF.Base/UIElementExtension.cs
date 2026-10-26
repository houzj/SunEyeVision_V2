using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Base;

public static class UIElementExtension
{
	private static readonly PropertyInfo ItemsHostPropertyInfo = typeof(ItemsControl).GetProperty("ItemsHost", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo EnsureGeneratorMethodInfo = typeof(Panel).GetMethod("EnsureGenerator", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly MethodInfo BringIndexIntoViewMethodInfo = typeof(VirtualizingPanel).GetMethod("BringIndexIntoView", BindingFlags.Instance | BindingFlags.NonPublic);

	public static Panel GetItemsHost(this ItemsControl itemsControl)
	{
		return ItemsHostPropertyInfo.GetValue(itemsControl, null) as Panel;
	}

	public static void CallEnsureGenerator(this Panel panel)
	{
		EnsureGeneratorMethodInfo.Invoke(panel, null);
	}

	public static void CallBringIndexIntoView(this VirtualizingPanel virtualizingPanel, int index)
	{
		BringIndexIntoViewMethodInfo.Invoke(virtualizingPanel, new object[1] { index });
	}

	public static T GetAncestor<T>(DependencyObject source) where T : DependencyObject
	{
		if (source == null)
		{
			return null;
		}
		do
		{
			source = VisualTreeHelper.GetParent(source);
		}
		while (source != null && !(source is T));
		return source as T;
	}
}
