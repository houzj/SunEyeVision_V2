using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Interactivity;

namespace VM.Utility;

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
