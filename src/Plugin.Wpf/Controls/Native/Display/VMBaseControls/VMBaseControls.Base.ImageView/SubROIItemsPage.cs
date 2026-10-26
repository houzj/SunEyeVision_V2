using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

public class SubROIItemsPage : ImageViewItemsPage
{
	static SubROIItemsPage()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(SubROIItemsPage), new FrameworkPropertyMetadata(typeof(SubROIItemsPage)));
	}

	protected override DependencyObject GetContainerForItemOverride()
	{
		return new ContainerItem
		{
			ParentItemsControl = this,
			HorizontalContentAlignment = HorizontalAlignment.Center,
			VerticalContentAlignment = VerticalAlignment.Center
		};
	}
}
