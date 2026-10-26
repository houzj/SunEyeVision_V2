using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

[TemplatePart(Name = "PART_NewItemTypesComboBox", Type = typeof(ComboBox))]
[TemplatePart(Name = "PART_PropertyGrid", Type = typeof(SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.PropertyGrid))]
[TemplatePart(Name = "PART_ListBox", Type = typeof(ListBox))]
public class CollectionControl : Control
{
	public delegate void ItemDeletingRoutedEventHandler(object sender, ItemDeletingEventArgs e);

	public delegate void ItemDeletedRoutedEventHandler(object sender, ItemEventArgs e);

	public delegate void ItemAddingRoutedEventHandler(object sender, ItemAddingEventArgs e);

	public delegate void ItemAddedRoutedEventHandler(object sender, ItemEventArgs e);

	public delegate void ItemMovedDownRoutedEventHandler(object sender, ItemEventArgs e);

	public delegate void ItemMovedUpRoutedEventHandler(object sender, ItemEventArgs e);

	private const string PART_NewItemTypesComboBox = "PART_NewItemTypesComboBox";

	private const string PART_PropertyGrid = "PART_PropertyGrid";

	private const string PART_ListBox = "PART_ListBox";

	private ComboBox _newItemTypesComboBox;

	private SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.PropertyGrid _propertyGrid;

	private ListBox _listBox;

	public static readonly DependencyProperty IsReadOnlyProperty;

	public static readonly DependencyProperty ItemsProperty;

	public static readonly DependencyProperty ItemsSourceProperty;

	public static readonly DependencyProperty ItemsSourceTypeProperty;

	public static readonly DependencyProperty NewItemTypesProperty;

	public static readonly DependencyProperty SelectedItemProperty;

	public static readonly RoutedEvent ItemDeletingEvent;

	public static readonly RoutedEvent ItemDeletedEvent;

	public static readonly RoutedEvent ItemAddingEvent;

	public static readonly RoutedEvent ItemAddedEvent;

	public static readonly RoutedEvent ItemMovedDownEvent;

	public static readonly RoutedEvent ItemMovedUpEvent;

	public bool IsReadOnly
	{
		get
		{
			return (bool)GetValue(IsReadOnlyProperty);
		}
		set
		{
			SetValue(IsReadOnlyProperty, value);
		}
	}

	public ObservableCollection<object> Items
	{
		get
		{
			return (ObservableCollection<object>)GetValue(ItemsProperty);
		}
		set
		{
			SetValue(ItemsProperty, value);
		}
	}

	public IEnumerable ItemsSource
	{
		get
		{
			return (IEnumerable)GetValue(ItemsSourceProperty);
		}
		set
		{
			SetValue(ItemsSourceProperty, value);
		}
	}

	public Type ItemsSourceType
	{
		get
		{
			return (Type)GetValue(ItemsSourceTypeProperty);
		}
		set
		{
			SetValue(ItemsSourceTypeProperty, value);
		}
	}

	public IList<Type> NewItemTypes
	{
		get
		{
			return (IList<Type>)GetValue(NewItemTypesProperty);
		}
		set
		{
			SetValue(NewItemTypesProperty, value);
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

	public SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.PropertyGrid PropertyGrid
	{
		get
		{
			if (_propertyGrid == null)
			{
				ApplyTemplate();
			}
			return _propertyGrid;
		}
	}

	public event ItemDeletingRoutedEventHandler ItemDeleting
	{
		add
		{
			AddHandler(ItemDeletingEvent, value);
		}
		remove
		{
			RemoveHandler(ItemDeletingEvent, value);
		}
	}

	public event ItemDeletedRoutedEventHandler ItemDeleted
	{
		add
		{
			AddHandler(ItemDeletedEvent, value);
		}
		remove
		{
			RemoveHandler(ItemDeletedEvent, value);
		}
	}

	public event ItemAddingRoutedEventHandler ItemAdding
	{
		add
		{
			AddHandler(ItemAddingEvent, value);
		}
		remove
		{
			RemoveHandler(ItemAddingEvent, value);
		}
	}

	public event ItemAddedRoutedEventHandler ItemAdded
	{
		add
		{
			AddHandler(ItemAddedEvent, value);
		}
		remove
		{
			RemoveHandler(ItemAddedEvent, value);
		}
	}

	public event ItemMovedDownRoutedEventHandler ItemMovedDown
	{
		add
		{
			AddHandler(ItemMovedDownEvent, value);
		}
		remove
		{
			RemoveHandler(ItemMovedDownEvent, value);
		}
	}

	public event ItemMovedUpRoutedEventHandler ItemMovedUp
	{
		add
		{
			AddHandler(ItemMovedUpEvent, value);
		}
		remove
		{
			RemoveHandler(ItemMovedUpEvent, value);
		}
	}

	private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((CollectionControl)d)?.OnItemSourceChanged((IEnumerable)e.OldValue, (IEnumerable)e.NewValue);
	}

	public void OnItemSourceChanged(IEnumerable oldValue, IEnumerable newValue)
	{
		if (newValue == null)
		{
			return;
		}
		if (newValue is IDictionary dictionary)
		{
			{
				foreach (DictionaryEntry item2 in dictionary)
				{
					Type keyType;
					if (item2.Key != null)
					{
						keyType = item2.Key.GetType();
					}
					else
					{
						keyType = ((dictionary.GetType().GetGenericArguments().Count() > 0) ? dictionary.GetType().GetGenericArguments()[0] : typeof(object));
					}
					Type valueType;
					if (item2.Value != null)
					{
						valueType = item2.Value.GetType();
					}
					else
					{
						valueType = ((dictionary.GetType().GetGenericArguments().Count() > 1) ? dictionary.GetType().GetGenericArguments()[1] : typeof(object));
					}
					object item = ListUtilities.CreateEditableKeyValuePair(item2.Key, keyType, item2.Value, valueType);
					Items.Add(item);
				}
				return;
			}
		}
		foreach (object item3 in newValue)
		{
			if (item3 != null)
			{
				Items.Add(item3);
			}
		}
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		if (_newItemTypesComboBox != null)
		{
			_newItemTypesComboBox.Loaded -= NewItemTypesComboBox_Loaded;
		}
		_newItemTypesComboBox = GetTemplateChild("PART_NewItemTypesComboBox") as ComboBox;
		if (_newItemTypesComboBox != null)
		{
			_newItemTypesComboBox.Loaded += NewItemTypesComboBox_Loaded;
		}
		_listBox = GetTemplateChild("PART_ListBox") as ListBox;
		if (_propertyGrid != null)
		{
			_propertyGrid.PropertyValueChanged -= PropertyGrid_PropertyValueChanged;
		}
		_propertyGrid = GetTemplateChild("PART_PropertyGrid") as SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.PropertyGrid;
		if (_propertyGrid != null)
		{
			_propertyGrid.PropertyValueChanged += PropertyGrid_PropertyValueChanged;
		}
	}

	static CollectionControl()
	{
		IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(CollectionControl), new UIPropertyMetadata(false));
		ItemsProperty = DependencyProperty.Register("Items", typeof(ObservableCollection<object>), typeof(CollectionControl), new UIPropertyMetadata(null));
		ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(CollectionControl), new UIPropertyMetadata(null, OnItemsSourceChanged));
		ItemsSourceTypeProperty = DependencyProperty.Register("ItemsSourceType", typeof(Type), typeof(CollectionControl), new UIPropertyMetadata(null));
		NewItemTypesProperty = DependencyProperty.Register("NewItemTypes", typeof(IList), typeof(CollectionControl), new UIPropertyMetadata(null));
		SelectedItemProperty = DependencyProperty.Register("SelectedItem", typeof(object), typeof(CollectionControl), new UIPropertyMetadata(null));
		ItemDeletingEvent = EventManager.RegisterRoutedEvent("ItemDeleting", RoutingStrategy.Bubble, typeof(ItemDeletingRoutedEventHandler), typeof(CollectionControl));
		ItemDeletedEvent = EventManager.RegisterRoutedEvent("ItemDeleted", RoutingStrategy.Bubble, typeof(ItemDeletedRoutedEventHandler), typeof(CollectionControl));
		ItemAddingEvent = EventManager.RegisterRoutedEvent("ItemAdding", RoutingStrategy.Bubble, typeof(ItemAddingRoutedEventHandler), typeof(CollectionControl));
		ItemAddedEvent = EventManager.RegisterRoutedEvent("ItemAdded", RoutingStrategy.Bubble, typeof(ItemAddedRoutedEventHandler), typeof(CollectionControl));
		ItemMovedDownEvent = EventManager.RegisterRoutedEvent("ItemMovedDown", RoutingStrategy.Bubble, typeof(ItemMovedDownRoutedEventHandler), typeof(CollectionControl));
		ItemMovedUpEvent = EventManager.RegisterRoutedEvent("ItemMovedUp", RoutingStrategy.Bubble, typeof(ItemMovedUpRoutedEventHandler), typeof(CollectionControl));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CollectionControl), new FrameworkPropertyMetadata(typeof(CollectionControl)));
	}

	public CollectionControl()
	{
		Items = new ObservableCollection<object>();
		CommandBindings.Add(new CommandBinding(ApplicationCommands.New, AddNew, CanAddNew));
		CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, Delete, CanDelete));
		CommandBindings.Add(new CommandBinding(ComponentCommands.MoveDown, MoveDown, CanMoveDown));
		CommandBindings.Add(new CommandBinding(ComponentCommands.MoveUp, MoveUp, CanMoveUp));
	}

	private void NewItemTypesComboBox_Loaded(object sender, RoutedEventArgs e)
	{
		if (_newItemTypesComboBox != null)
		{
			_newItemTypesComboBox.SelectedIndex = 0;
		}
	}

	private void PropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e)
	{
		if (_listBox != null)
		{
			_listBox.Items.Refresh();
		}
	}

	private void AddNew(object sender, ExecutedRoutedEventArgs e)
	{
		object obj = CreateNewItem((Type)e.Parameter);
		PropertyInfo[] properties = obj.GetType().GetProperties();
		foreach (PropertyInfo propertyInfo in properties)
		{
			if (propertyInfo.CanWrite && propertyInfo.PropertyType.IsGenericType)
			{
				object value = Activator.CreateInstance(propertyInfo.PropertyType);
				propertyInfo.SetValue(obj, value, null);
			}
		}
		ItemAddingEventArgs e2 = new ItemAddingEventArgs(ItemAddingEvent, obj);
		RaiseEvent(e2);
		if (!e2.Cancel)
		{
			obj = e2.Item;
			Items.Add(obj);
			RaiseEvent(new ItemEventArgs(ItemAddedEvent, obj));
			SelectedItem = obj;
		}
	}

	private void CanAddNew(object sender, CanExecuteRoutedEventArgs e)
	{
		Type type = e.Parameter as Type;
		if (type != null && !IsReadOnly && ((type.IsValueType && !type.IsEnum && !type.IsPrimitive) || type.GetConstructor(Type.EmptyTypes) != null))
		{
			e.CanExecute = true;
		}
	}

	private void Delete(object sender, ExecutedRoutedEventArgs e)
	{
		ItemDeletingEventArgs e2 = new ItemDeletingEventArgs(ItemDeletingEvent, e.Parameter);
		RaiseEvent(e2);
		if (!e2.Cancel)
		{
			Items.Remove(e.Parameter);
			RaiseEvent(new ItemEventArgs(ItemDeletedEvent, e.Parameter));
		}
	}

	private void CanDelete(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = e.Parameter != null && !IsReadOnly;
	}

	private void MoveDown(object sender, ExecutedRoutedEventArgs e)
	{
		object parameter = e.Parameter;
		int num = Items.IndexOf(parameter);
		Items.RemoveAt(num);
		Items.Insert(++num, parameter);
		RaiseEvent(new ItemEventArgs(ItemMovedDownEvent, parameter));
		SelectedItem = parameter;
	}

	private void CanMoveDown(object sender, CanExecuteRoutedEventArgs e)
	{
		if (e.Parameter != null && Items.IndexOf(e.Parameter) < Items.Count - 1 && !IsReadOnly)
		{
			e.CanExecute = true;
		}
	}

	private void MoveUp(object sender, ExecutedRoutedEventArgs e)
	{
		object parameter = e.Parameter;
		int num = Items.IndexOf(parameter);
		Items.RemoveAt(num);
		Items.Insert(--num, parameter);
		RaiseEvent(new ItemEventArgs(ItemMovedUpEvent, parameter));
		SelectedItem = parameter;
	}

	private void CanMoveUp(object sender, CanExecuteRoutedEventArgs e)
	{
		if (e.Parameter != null && Items.IndexOf(e.Parameter) > 0 && !IsReadOnly)
		{
			e.CanExecute = true;
		}
	}

	public void PersistChanges()
	{
		PersistChanges(Items);
	}

	internal void PersistChanges(IList sourceList)
	{
		IEnumerable enumerable = ComputeItemsSource();
		if (enumerable == null)
		{
			return;
		}
		if (enumerable is IDictionary)
		{
			IDictionary dictionary = (IDictionary)enumerable;
			dictionary.Clear();
			{
				foreach (object source in sourceList)
				{
					PropertyInfo property = source.GetType().GetProperty("Key");
					PropertyInfo property2 = source.GetType().GetProperty("Value");
					if (property != null && property2 != null)
					{
						dictionary.Add(property.GetValue(source, null), property2.GetValue(source, null));
					}
				}
				return;
			}
		}
		if (enumerable is IList)
		{
			IList list = (IList)enumerable;
			list.Clear();
			if (list.IsFixedSize)
			{
				for (int i = 0; i < sourceList.Count; i++)
				{
					list[i] = sourceList[i];
				}
				return;
			}
			{
				foreach (object source2 in sourceList)
				{
					list.Add(source2);
				}
				return;
			}
		}
		Type type = enumerable.GetType().GetInterfaces().FirstOrDefault((Type x) => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>));
		if (!(type != null))
		{
			return;
		}
		Type type2 = type.GetGenericArguments().FirstOrDefault();
		if (!(type2 != null))
		{
			return;
		}
		Type type3 = typeof(ICollection<>).MakeGenericType(type2);
		type3.GetMethod("Clear").Invoke(enumerable, null);
		foreach (object source3 in sourceList)
		{
			type3.GetMethod("Add").Invoke(enumerable, new object[1] { source3 });
		}
	}

	private IEnumerable CreateItemsSource()
	{
		IEnumerable result = null;
		if (ItemsSourceType != null)
		{
			ConstructorInfo constructor = ItemsSourceType.GetConstructor(Type.EmptyTypes);
			if (constructor != null)
			{
				result = (IEnumerable)constructor.Invoke(null);
			}
		}
		return result;
	}

	private object CreateNewItem(Type type)
	{
		return Activator.CreateInstance(type);
	}

	private IEnumerable ComputeItemsSource()
	{
		if (ItemsSource == null)
		{
			ItemsSource = CreateItemsSource();
		}
		return ItemsSource;
	}
}
