using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

public class Selector : ItemsControl, IWeakEventListener
{
	private class ValueEqualityComparer : IEqualityComparer<string>
	{
		public bool Equals(string x, string y)
		{
			return string.Equals(x, y, StringComparison.InvariantCultureIgnoreCase);
		}

		public int GetHashCode(string obj)
		{
			return 1;
		}
	}

	private bool _surpressItemSelectionChanged;

	private bool _ignoreSelectedItemChanged;

	private bool _ignoreSelectedValueChanged;

	private int _ignoreSelectedItemsCollectionChanged;

	private int _ignoreSelectedMemberPathValuesChanged;

	private IList _selectedItems;

	private IList _removedItems = new ObservableCollection<object>();

	private ValueChangeHelper _selectedMemberPathValuesHelper;

	private ValueChangeHelper _valueMemberPathValuesHelper;

	public static readonly DependencyProperty CommandProperty = DependencyProperty.Register("Command", typeof(ICommand), typeof(Selector), new PropertyMetadata((object)null));

	public static readonly DependencyProperty DelimiterProperty = DependencyProperty.Register("Delimiter", typeof(string), typeof(Selector), new UIPropertyMetadata(",", OnDelimiterChanged));

	public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register("SelectedItem", typeof(object), typeof(Selector), new UIPropertyMetadata(null, OnSelectedItemChanged));

	public static readonly DependencyProperty SelectedItemsOverrideProperty = DependencyProperty.Register("SelectedItemsOverride", typeof(IList), typeof(Selector), new UIPropertyMetadata(null, SelectedItemsOverrideChanged));

	public static readonly DependencyProperty SelectedMemberPathProperty = DependencyProperty.Register("SelectedMemberPath", typeof(string), typeof(Selector), new UIPropertyMetadata(null, OnSelectedMemberPathChanged));

	public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register("SelectedValue", typeof(string), typeof(Selector), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedValueChanged));

	public static readonly DependencyProperty ValueMemberPathProperty = DependencyProperty.Register("ValueMemberPath", typeof(string), typeof(Selector), new UIPropertyMetadata(OnValueMemberPathChanged));

	public static readonly RoutedEvent SelectedEvent = EventManager.RegisterRoutedEvent("SelectedEvent", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Selector));

	public static readonly RoutedEvent UnSelectedEvent = EventManager.RegisterRoutedEvent("UnSelectedEvent", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Selector));

	public static readonly RoutedEvent ItemSelectionChangedEvent = EventManager.RegisterRoutedEvent("ItemSelectionChanged", RoutingStrategy.Bubble, typeof(ItemSelectionChangedEventHandler), typeof(Selector));

	[TypeConverter(typeof(CommandConverter))]
	public ICommand Command
	{
		get
		{
			return (ICommand)GetValue(CommandProperty);
		}
		set
		{
			SetValue(CommandProperty, value);
		}
	}

	public string Delimiter
	{
		get
		{
			return (string)GetValue(DelimiterProperty);
		}
		set
		{
			SetValue(DelimiterProperty, value);
		}
	}

	public object SelectedItem
	{
		get
		{
			return GetValue(SelectedItemProperty);
		}
		set
		{
			SetValue(SelectedItemProperty, value);
		}
	}

	public IList SelectedItems
	{
		get
		{
			return _selectedItems;
		}
		private set
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			INotifyCollectionChanged notifyCollectionChanged = _selectedItems as INotifyCollectionChanged;
			INotifyCollectionChanged notifyCollectionChanged2 = value as INotifyCollectionChanged;
			if (notifyCollectionChanged != null)
			{
				CollectionChangedEventManager.RemoveListener(notifyCollectionChanged, this);
			}
			if (notifyCollectionChanged2 != null)
			{
				CollectionChangedEventManager.AddListener(notifyCollectionChanged2, this);
			}
			_selectedItems = value;
		}
	}

	public IList SelectedItemsOverride
	{
		get
		{
			return (IList)GetValue(SelectedItemsOverrideProperty);
		}
		set
		{
			SetValue(SelectedItemsOverrideProperty, value);
		}
	}

	public string SelectedMemberPath
	{
		get
		{
			return (string)GetValue(SelectedMemberPathProperty);
		}
		set
		{
			SetValue(SelectedMemberPathProperty, value);
		}
	}

	public string SelectedValue
	{
		get
		{
			return (string)GetValue(SelectedValueProperty);
		}
		set
		{
			SetValue(SelectedValueProperty, value);
		}
	}

	public string ValueMemberPath
	{
		get
		{
			return (string)GetValue(ValueMemberPathProperty);
		}
		set
		{
			SetValue(ValueMemberPathProperty, value);
		}
	}

	protected IEnumerable ItemsCollection
	{
		get
		{
			object obj = ItemsSource;
			if (obj == null)
			{
				IEnumerable items = Items;
				obj = items ?? new object[0];
			}
			return (IEnumerable)obj;
		}
	}

	public event ItemSelectionChangedEventHandler ItemSelectionChanged
	{
		add
		{
			AddHandler(ItemSelectionChangedEvent, value);
		}
		remove
		{
			RemoveHandler(ItemSelectionChangedEvent, value);
		}
	}

	public Selector()
	{
		SelectedItems = new ObservableCollection<object>();
		AddHandler(SelectedEvent, (RoutedEventHandler)((object s, RoutedEventArgs args) =>
		{
			OnItemSelectionChangedCore(args, unselected: false);
		}));
		AddHandler(UnSelectedEvent, (RoutedEventHandler)((object s, RoutedEventArgs args) =>
		{
			OnItemSelectionChangedCore(args, unselected: true);
		}));
		_selectedMemberPathValuesHelper = new ValueChangeHelper(OnSelectedMemberPathValuesChanged);
		_valueMemberPathValuesHelper = new ValueChangeHelper(OnValueMemberPathValuesChanged);
	}

	private static void OnDelimiterChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		((Selector)o).OnSelectedItemChanged((string)e.OldValue, (string)e.NewValue);
	}

	protected virtual void OnSelectedItemChanged(string oldValue, string newValue)
	{
		if (IsInitialized)
		{
			UpdateSelectedValue();
		}
	}

	private static void OnSelectedItemChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
	{
		((Selector)sender).OnSelectedItemChanged(args.OldValue, args.NewValue);
	}

	protected virtual void OnSelectedItemChanged(object oldValue, object newValue)
	{
		if (IsInitialized && !_ignoreSelectedItemChanged)
		{
			_ignoreSelectedItemsCollectionChanged++;
			SelectedItems.Clear();
			if (newValue != null)
			{
				SelectedItems.Add(newValue);
			}
			UpdateFromSelectedItems();
			_ignoreSelectedItemsCollectionChanged--;
		}
	}

	private static void SelectedItemsOverrideChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
	{
		((Selector)sender).OnSelectedItemsOverrideChanged((IList)args.OldValue, (IList)args.NewValue);
	}

	protected virtual void OnSelectedItemsOverrideChanged(IList oldValue, IList newValue)
	{
		if (IsInitialized)
		{
			IList selectedItems;
			if (newValue == null)
			{
				IList list = new ObservableCollection<object>();
				selectedItems = list;
			}
			else
			{
				selectedItems = newValue;
			}
			SelectedItems = selectedItems;
			UpdateFromSelectedItems();
		}
	}

	private static void OnSelectedMemberPathChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		((Selector)o).OnSelectedMemberPathChanged((string)e.OldValue, (string)e.NewValue);
	}

	protected virtual void OnSelectedMemberPathChanged(string oldValue, string newValue)
	{
		if (IsInitialized)
		{
			UpdateSelectedMemberPathValuesBindings();
		}
	}

	private static void OnSelectedValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is Selector selector)
		{
			selector.OnSelectedValueChanged((string)e.OldValue, (string)e.NewValue);
		}
	}

	protected virtual void OnSelectedValueChanged(string oldValue, string newValue)
	{
		if (IsInitialized && !_ignoreSelectedValueChanged)
		{
			UpdateFromSelectedValue();
		}
	}

	private static void OnValueMemberPathChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		((Selector)o).OnValueMemberPathChanged((string)e.OldValue, (string)e.NewValue);
	}

	protected virtual void OnValueMemberPathChanged(string oldValue, string newValue)
	{
		if (IsInitialized)
		{
			UpdateValueMemberPathValuesBindings();
		}
	}

	protected override bool IsItemItsOwnContainerOverride(object item)
	{
		return item is SelectorItem;
	}

	protected override DependencyObject GetContainerForItemOverride()
	{
		return new SelectorItem();
	}

	protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
	{
		base.PrepareContainerForItemOverride(element, item);
		_surpressItemSelectionChanged = true;
		(element as FrameworkElement).SetValue(SelectorItem.IsSelectedProperty, SelectedItems.Contains(item));
		_surpressItemSelectionChanged = false;
	}

	protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
	{
		base.OnItemsSourceChanged(oldValue, newValue);
		INotifyCollectionChanged notifyCollectionChanged = oldValue as INotifyCollectionChanged;
		INotifyCollectionChanged notifyCollectionChanged2 = newValue as INotifyCollectionChanged;
		if (notifyCollectionChanged != null)
		{
			CollectionChangedEventManager.RemoveListener(notifyCollectionChanged, this);
		}
		if (notifyCollectionChanged2 != null)
		{
			CollectionChangedEventManager.AddListener(notifyCollectionChanged2, this);
		}
		if (IsInitialized)
		{
			if (!VirtualizingPanel.GetIsVirtualizing(this) || (VirtualizingPanel.GetIsVirtualizing(this) && newValue != null))
			{
				RemoveUnavailableSelectedItems();
			}
			UpdateSelectedMemberPathValuesBindings();
			UpdateValueMemberPathValuesBindings();
		}
	}

	public override void EndInit()
	{
		base.EndInit();
		if (SelectedItemsOverride != null)
		{
			OnSelectedItemsOverrideChanged(null, SelectedItemsOverride);
		}
		else if (SelectedMemberPath != null)
		{
			OnSelectedMemberPathChanged(null, SelectedMemberPath);
		}
		else if (SelectedValue != null)
		{
			OnSelectedValueChanged(null, SelectedValue);
		}
		else if (SelectedItem != null)
		{
			OnSelectedItemChanged(null, SelectedItem);
		}
		if (ValueMemberPath != null)
		{
			OnValueMemberPathChanged(null, ValueMemberPath);
		}
	}

	protected object GetPathValue(object item, string propertyPath)
	{
		if (item == null)
		{
			throw new ArgumentNullException("item");
		}
		if (string.IsNullOrEmpty(propertyPath) || propertyPath == ".")
		{
			return item;
		}
		PropertyInfo property = item.GetType().GetProperty(propertyPath);
		if (!(property != null))
		{
			return null;
		}
		return property.GetValue(item, null);
	}

	protected object GetItemValue(object item)
	{
		if (item == null)
		{
			return null;
		}
		return GetPathValue(item, ValueMemberPath);
	}

	protected object ResolveItemByValue(string value)
	{
		if (!string.IsNullOrEmpty(ValueMemberPath))
		{
			foreach (object item in ItemsCollection)
			{
				PropertyInfo property = item.GetType().GetProperty(ValueMemberPath);
				if (property != null)
				{
					object value2 = property.GetValue(item, null);
					if (value.Equals(value2.ToString(), StringComparison.InvariantCultureIgnoreCase))
					{
						return item;
					}
				}
			}
		}
		return value;
	}

	internal void UpdateFromList(List<string> selectedValues, Func<object, object> GetItemfunction)
	{
		_ignoreSelectedItemsCollectionChanged++;
		SelectedItems.Clear();
		if (selectedValues != null && selectedValues.Count > 0)
		{
			ValueEqualityComparer comparer = new ValueEqualityComparer();
			foreach (object item in ItemsCollection)
			{
				object obj = GetItemfunction(item);
				if (obj != null && selectedValues.Contains(obj.ToString(), comparer))
				{
					SelectedItems.Add(item);
				}
			}
		}
		_ignoreSelectedItemsCollectionChanged--;
		UpdateFromSelectedItems();
	}

	private bool? GetSelectedMemberPathValue(object item)
	{
		if (string.IsNullOrEmpty(SelectedMemberPath))
		{
			return null;
		}
		if (item == null)
		{
			return null;
		}
		string[] array = SelectedMemberPath.Split('.');
		if (array.Length == 1)
		{
			PropertyInfo property = item.GetType().GetProperty(SelectedMemberPath);
			if (property != null && property.PropertyType == typeof(bool))
			{
				return property.GetValue(item, null) as bool?;
			}
			return null;
		}
		for (int i = 0; i < array.Count(); i++)
		{
			PropertyInfo property2 = item.GetType().GetProperty(array[i]);
			if (property2 == null)
			{
				return null;
			}
			if (i == array.Count() - 1)
			{
				if (property2.PropertyType == typeof(bool))
				{
					return property2.GetValue(item, null) as bool?;
				}
			}
			else
			{
				item = property2.GetValue(item, null);
			}
		}
		return null;
	}

	private void SetSelectedMemberPathValue(object item, bool value)
	{
		if (string.IsNullOrEmpty(SelectedMemberPath) || item == null)
		{
			return;
		}
		string[] array = SelectedMemberPath.Split('.');
		if (array.Length == 1)
		{
			PropertyInfo property = item.GetType().GetProperty(SelectedMemberPath);
			if (property != null && property.PropertyType == typeof(bool))
			{
				property.SetValue(item, value, null);
			}
			return;
		}
		for (int i = 0; i < array.Count(); i++)
		{
			PropertyInfo property2 = item.GetType().GetProperty(array[i]);
			if (property2 == null)
			{
				break;
			}
			if (i == array.Count() - 1)
			{
				if (property2.PropertyType == typeof(bool))
				{
					property2.SetValue(item, value, null);
				}
			}
			else
			{
				item = property2.GetValue(item, null);
			}
		}
	}

	protected virtual void OnSelectedItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (_ignoreSelectedItemsCollectionChanged <= 0)
		{
			UpdateFromSelectedItems();
		}
	}

	private void OnItemSelectionChangedCore(RoutedEventArgs args, bool unselected)
	{
		object obj = ItemContainerGenerator.ItemFromContainer((DependencyObject)args.OriginalSource);
		if (obj == DependencyProperty.UnsetValue)
		{
			obj = args.OriginalSource;
		}
		if (unselected)
		{
			while (SelectedItems.Contains(obj))
			{
				SelectedItems.Remove(obj);
			}
		}
		else if (!SelectedItems.Contains(obj))
		{
			SelectedItems.Add(obj);
		}
		OnItemSelectionChanged(new ItemSelectionChangedEventArgs(ItemSelectionChangedEvent, this, obj, !unselected));
	}

	private void OnItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
	{
		RemoveUnavailableSelectedItems();
		AddAvailableRemovedItems();
		UpdateSelectedMemberPathValuesBindings();
		UpdateValueMemberPathValuesBindings();
	}

	private void OnSelectedMemberPathValuesChanged()
	{
		if (_ignoreSelectedMemberPathValuesChanged <= 0)
		{
			UpdateFromSelectedMemberPathValues();
		}
	}

	private void OnValueMemberPathValuesChanged()
	{
		UpdateSelectedValue();
	}

	private void UpdateSelectedMemberPathValuesBindings()
	{
		_selectedMemberPathValuesHelper.UpdateValueSource(ItemsCollection, SelectedMemberPath);
		UpdateFromSelectedMemberPathValues();
	}

	private void UpdateValueMemberPathValuesBindings()
	{
		_valueMemberPathValuesHelper.UpdateValueSource(ItemsCollection, ValueMemberPath);
	}

	protected virtual void OnItemSelectionChanged(ItemSelectionChangedEventArgs args)
	{
		if (!_surpressItemSelectionChanged)
		{
			RaiseEvent(args);
			if (Command != null)
			{
				Command.Execute(args.Item);
			}
		}
	}

	private void UpdateSelectedValue()
	{
		string text = string.Join(Delimiter, from object x in SelectedItems
			select GetItemValue(x));
		if (string.IsNullOrEmpty(SelectedValue) || !SelectedValue.Equals(text))
		{
			_ignoreSelectedValueChanged = true;
			SelectedValue = text;
			_ignoreSelectedValueChanged = false;
		}
	}

	private void UpdateSelectedItem()
	{
		if (!SelectedItems.Contains(SelectedItem))
		{
			_ignoreSelectedItemChanged = true;
			SelectedItem = ((SelectedItems.Count > 0) ? SelectedItems[0] : null);
			_ignoreSelectedItemChanged = false;
		}
	}

	private void UpdateFromSelectedMemberPathValues()
	{
		_ignoreSelectedItemsCollectionChanged++;
		foreach (object item in ItemsCollection)
		{
			bool? selectedMemberPathValue = GetSelectedMemberPathValue(item);
			if (!selectedMemberPathValue.HasValue)
			{
				continue;
			}
			if (selectedMemberPathValue.Value)
			{
				if (!SelectedItems.Contains(item))
				{
					SelectedItems.Add(item);
				}
			}
			else if (SelectedItems.Contains(item))
			{
				SelectedItems.Remove(item);
			}
		}
		_ignoreSelectedItemsCollectionChanged--;
		UpdateFromSelectedItems();
	}

	internal void UpdateSelectedItems(IList selectedItems)
	{
		if (selectedItems == null)
		{
			throw new ArgumentNullException("selectedItems");
		}
		if (selectedItems.Count == SelectedItems.Count && selectedItems.Cast<object>().SequenceEqual(SelectedItems.Cast<object>()))
		{
			return;
		}
		_ignoreSelectedItemsCollectionChanged++;
		SelectedItems.Clear();
		foreach (object selectedItem in selectedItems)
		{
			SelectedItems.Add(selectedItem);
		}
		_ignoreSelectedItemsCollectionChanged--;
		UpdateFromSelectedItems();
	}

	private void UpdateFromSelectedItems()
	{
		foreach (object item in ItemsCollection)
		{
			bool flag = SelectedItems.Contains(item);
			_ignoreSelectedMemberPathValuesChanged++;
			SetSelectedMemberPathValue(item, flag);
			_ignoreSelectedMemberPathValuesChanged--;
			if (ItemContainerGenerator.ContainerFromItem(item) is SelectorItem selectorItem)
			{
				selectorItem.IsSelected = flag;
			}
		}
		UpdateSelectedItem();
		UpdateSelectedValue();
	}

	private void RemoveUnavailableSelectedItems()
	{
		_ignoreSelectedItemsCollectionChanged++;
		HashSet<object> hashSet = new HashSet<object>(ItemsCollection.Cast<object>());
		for (int i = 0; i < SelectedItems.Count; i++)
		{
			if (!hashSet.Contains(SelectedItems[i]))
			{
				_removedItems.Add(SelectedItems[i]);
				SelectedItems.RemoveAt(i);
				i--;
			}
		}
		_ignoreSelectedItemsCollectionChanged--;
		UpdateSelectedItem();
		UpdateSelectedValue();
	}

	private void AddAvailableRemovedItems()
	{
		HashSet<object> hashSet = new HashSet<object>(ItemsCollection.Cast<object>());
		for (int i = 0; i < _removedItems.Count; i++)
		{
			if (hashSet.Contains(_removedItems[i]))
			{
				SelectedItems.Add(_removedItems[i]);
				_removedItems.RemoveAt(i);
				i--;
			}
		}
	}

	private void UpdateFromSelectedValue()
	{
		List<string> selectedValues = null;
		if (!string.IsNullOrEmpty(SelectedValue))
		{
			selectedValues = SelectedValue.Split(new string[1] { Delimiter }, StringSplitOptions.RemoveEmptyEntries).ToList();
		}
		UpdateFromList(selectedValues, GetItemValue);
	}

	public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
	{
		if (managerType == typeof(CollectionChangedEventManager))
		{
			if (_selectedItems == sender)
			{
				OnSelectedItemsCollectionChanged(sender, (NotifyCollectionChangedEventArgs)e);
				return true;
			}
			if (ItemsCollection == sender)
			{
				OnItemsSourceCollectionChanged(sender, (NotifyCollectionChangedEventArgs)e);
				return true;
			}
		}
		return false;
	}
}
