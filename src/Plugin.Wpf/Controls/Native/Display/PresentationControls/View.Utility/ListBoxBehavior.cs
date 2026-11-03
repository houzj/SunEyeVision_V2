using SunEyeVision.Plugin.Wpf.Native.Dependencies.View.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.ViewUtility;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Collections.Specialized;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ViewUtility;

public class ListBoxBehavior : Behavior<ListBox>
{
	protected override void OnAttached()
	{
		base.OnAttached();
		((INotifyCollectionChanged)base.AssociatedObject.Items).CollectionChanged += ListBoxScrollToBottomBehavior_CollectionChanged;
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();
		((INotifyCollectionChanged)base.AssociatedObject.Items).CollectionChanged -= ListBoxScrollToBottomBehavior_CollectionChanged;
	}

	private void ListBoxScrollToBottomBehavior_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (base.AssociatedObject.HasItems)
		{
			base.AssociatedObject.ScrollIntoView(base.AssociatedObject.Items[base.AssociatedObject.Items.Count - 1]);
		}
	}
}
