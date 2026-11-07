using System.Windows;

namespace VMBaseControls.Base.ImageView;

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
