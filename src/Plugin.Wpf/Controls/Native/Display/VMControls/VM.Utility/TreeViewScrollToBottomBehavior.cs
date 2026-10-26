using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections.Specialized;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;

public class TreeViewScrollToBottomBehavior : Behavior<TreeView>
{
	protected override void OnAttached()
	{
		base.OnAttached();
		((INotifyCollectionChanged)base.AssociatedObject.Items).CollectionChanged += TreeViewScrollToBottomBehavior_CollectionChanged;
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();
		((INotifyCollectionChanged)base.AssociatedObject.Items).CollectionChanged -= TreeViewScrollToBottomBehavior_CollectionChanged;
	}

	private void TreeViewScrollToBottomBehavior_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		TreeView associatedObject;
		if (base.AssociatedObject.HasItems && (associatedObject = base.AssociatedObject) != null)
		{
			TreeViewAutomationPeer treeViewAutomationPeer = new TreeViewAutomationPeer(associatedObject);
			if (treeViewAutomationPeer != null && treeViewAutomationPeer.GetPattern(PatternInterface.Scroll) is ScrollViewerAutomationPeer scrollViewerAutomationPeer && scrollViewerAutomationPeer != null && scrollViewerAutomationPeer.Owner is ScrollViewer scrollViewer)
			{
				scrollViewer.ScrollToEnd();
			}
		}
	}
}
