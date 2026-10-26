using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

[TemplatePart(Name = "PART_ItemsHolder", Type = typeof(Panel))]
public class TabControlEx : TabControl, IDisposable
{
	private Panel _itemsHolder = null;

	private object _deletedObject = null;

	public TabControlEx()
	{
		ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
	}

	private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
	{
		if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
		{
			ItemContainerGenerator.StatusChanged -= ItemContainerGenerator_StatusChanged;
			UpdateSelectedItem();
		}
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		_itemsHolder = GetTemplateChild("PART_ItemsHolder") as Panel;
		UpdateSelectedItem();
	}

	protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
	{
		base.OnItemsChanged(e);
		if (_itemsHolder == null)
		{
			return;
		}
		switch (e.Action)
		{
		case NotifyCollectionChangedAction.Reset:
			_itemsHolder.Children.Clear();
			if (Items.Count > 0)
			{
				SelectedItem = Items[0];
				UpdateSelectedItem();
			}
			break;
		case NotifyCollectionChangedAction.Add:
		case NotifyCollectionChangedAction.Remove:
			if (e.NewItems != null && _deletedObject != null)
			{
				foreach (object newItem in e.NewItems)
				{
					if (_deletedObject == newItem)
					{
						ContentPresenter contentPresenter = FindChildContentPresenter(_deletedObject);
						if (contentPresenter != null)
						{
							int index = _itemsHolder.Children.IndexOf(contentPresenter);
							(_itemsHolder.Children[index] as ContentPresenter).Tag = ((newItem is TabItem) ? newItem : ItemContainerGenerator.ContainerFromItem(newItem));
						}
						_deletedObject = null;
					}
				}
			}
			if (e.OldItems != null)
			{
				foreach (object oldItem in e.OldItems)
				{
					_deletedObject = oldItem;
					Dispatcher.BeginInvoke(DispatcherPriority.DataBind, (Action)(() =>
					{
						if (_deletedObject != null)
						{
							ContentPresenter contentPresenter2 = FindChildContentPresenter(_deletedObject);
							if (contentPresenter2 != null)
							{
								_itemsHolder.Children.Remove(contentPresenter2);
							}
						}
					}));
				}
			}
			UpdateSelectedItem();
			break;
		case NotifyCollectionChangedAction.Replace:
			Dispatcher.BeginInvoke(DispatcherPriority.DataBind, (Action)(() =>
			{
				if (e.OldItems[0] != null)
				{
					ContentPresenter contentPresenter2 = FindChildContentPresenter(e.OldItems[0]);
					if (contentPresenter2 != null)
					{
						_itemsHolder.Children.Remove(contentPresenter2);
						UpdateSelectedItem();
					}
				}
			}));
			break;
		case NotifyCollectionChangedAction.Move:
			break;
		}
	}

	protected override void OnSelectionChanged(SelectionChangedEventArgs e)
	{
		base.OnSelectionChanged(e);
		UpdateSelectedItem();
	}

	private void UpdateSelectedItem()
	{
		if (_itemsHolder == null)
		{
			return;
		}
		TabItem selectedTabItem = GetSelectedTabItem();
		if (selectedTabItem != null)
		{
			CreateChildContentPresenter(selectedTabItem);
		}
		foreach (ContentPresenter child in _itemsHolder.Children)
		{
			child.Visibility = ((!(child.Tag as TabItem).IsSelected) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private ContentPresenter CreateChildContentPresenter(object item)
	{
		if (item == null)
		{
			return null;
		}
		ContentPresenter contentPresenter = FindChildContentPresenter(item);
		if (contentPresenter != null)
		{
			return contentPresenter;
		}
		contentPresenter = new ContentPresenter();
		contentPresenter.Content = ((item is TabItem) ? (item as TabItem).Content : item);
		contentPresenter.ContentTemplate = ContentTemplate;
		contentPresenter.ContentTemplateSelector = SelectedContentTemplateSelector;
		contentPresenter.ContentStringFormat = SelectedContentStringFormat;
		contentPresenter.Visibility = Visibility.Collapsed;
		contentPresenter.Tag = ((item is TabItem) ? item : ItemContainerGenerator.ContainerFromItem(item));
		_itemsHolder.Children.Add(contentPresenter);
		return contentPresenter;
	}

	private ContentPresenter FindChildContentPresenter(object data)
	{
		if (data is TabItem)
		{
			data = (data as TabItem).Content;
		}
		if (data == null)
		{
			return null;
		}
		if (_itemsHolder == null)
		{
			return null;
		}
		foreach (ContentPresenter child in _itemsHolder.Children)
		{
			if (child.Content == data)
			{
				return child;
			}
		}
		return null;
	}

	protected TabItem GetSelectedTabItem()
	{
		object selectedItem = SelectedItem;
		if (selectedItem == null)
		{
			return null;
		}
		if (_deletedObject == selectedItem)
		{
		}
		if (SelectedIndex < 0)
		{
			return null;
		}
		TabItem tabItem = selectedItem as TabItem;
		if (tabItem == null)
		{
			tabItem = ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as TabItem;
		}
		return tabItem;
	}

	public void Dispose()
	{
		foreach (object item in ItemContainerGenerator.Items)
		{
			ContentPresenter contentPresenter = FindChildContentPresenter(item);
			if (contentPresenter != null)
			{
				contentPresenter.Content = null;
			}
		}
	}
}
