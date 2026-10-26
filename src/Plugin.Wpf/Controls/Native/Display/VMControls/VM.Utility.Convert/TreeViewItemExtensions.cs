using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMUtility.Convert;

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
