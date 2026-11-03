using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView;

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
