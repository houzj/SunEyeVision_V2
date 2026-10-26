using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

public class CategoryGroupStyleSelector : StyleSelector
{
	public Style SingleDefaultCategoryItemGroupStyle { get; set; }

	public Style ItemGroupStyle { get; set; }

	public override Style SelectStyle(object item, DependencyObject container)
	{
		CollectionViewGroup collectionViewGroup = item as CollectionViewGroup;
		if (collectionViewGroup.Name != null && !collectionViewGroup.Name.Equals(CategoryAttribute.Default.Category))
		{
			return ItemGroupStyle;
		}
		while (container != null)
		{
			container = VisualTreeHelper.GetParent(container);
			if (container is ItemsControl)
			{
				break;
			}
		}
		if (container is ItemsControl itemsControl && itemsControl.Items.Count > 0 && itemsControl.Items.Groups.Count == 1)
		{
			return SingleDefaultCategoryItemGroupStyle;
		}
		return ItemGroupStyle;
	}
}
