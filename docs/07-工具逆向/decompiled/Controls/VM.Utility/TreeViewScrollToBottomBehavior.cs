using System.Collections.Specialized;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interactivity;

namespace VM.Utility;

public class TreeViewScrollToBottomBehavior : Behavior<TreeView>
{
	protected override void OnAttached()
	{
		((Behavior)this).OnAttached();
		((INotifyCollectionChanged)base.AssociatedObject.Items).CollectionChanged += TreeViewScrollToBottomBehavior_CollectionChanged;
	}

	protected override void OnDetaching()
	{
		((Behavior)this).OnDetaching();
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
