using SunEyeVision.Plugin.Wpf.Native.Dependencies.View.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.ViewUtility;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Collections.Specialized;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ViewUtility;

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
