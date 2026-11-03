using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Platform.Convert;
using SunEyeVision.Plugin.Wpf.Converters.Tree;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Converters.Tree;

public static class TreeViewItemExtensions
{
	public static int GetDepth(this TreeViewItem item)
	{
		TreeViewItem parent;
		if ((parent = GetParent(item)) != null)
		{
			return parent.GetDepth() + 1;
		}
		return 0;
	}

	private static TreeViewItem GetParent(TreeViewItem item)
	{
		DependencyObject parent = VisualTreeHelper.GetParent(item);
		while (!(parent is TreeViewItem) && !(parent is TreeView))
		{
			parent = VisualTreeHelper.GetParent(parent);
		}
		return parent as TreeViewItem;
	}
}
