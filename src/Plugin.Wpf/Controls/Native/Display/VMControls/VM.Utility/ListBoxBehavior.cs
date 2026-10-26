using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections.Specialized;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;

public class ListBoxBehavior : Behavior<ListBox>
{
	protected override void OnAttached()
	{
		((Behavior)this).OnAttached();
		((INotifyCollectionChanged)base.AssociatedObject.Items).CollectionChanged += ListBoxScrollToBottomBehavior_CollectionChanged;
	}

	protected override void OnDetaching()
	{
		((Behavior)this).OnDetaching();
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
