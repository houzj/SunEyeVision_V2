using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class PrimitiveTypeCollectionControl : ContentControl
{
	private bool _surpressTextChanged;

	private bool _conversionFailed;

	public static readonly DependencyProperty IsOpenProperty;

	public static readonly DependencyProperty ItemsSourceProperty;

	public static readonly DependencyProperty IsReadOnlyProperty;

	public static readonly DependencyProperty ItemsSourceTypeProperty;

	public static readonly DependencyProperty ItemTypeProperty;

	public static readonly DependencyProperty TextProperty;

	public bool IsOpen
	{
		get
		{
			return (bool)GetValue(IsOpenProperty);
		}
		set
		{
			SetValue(IsOpenProperty, value);
		}
	}

	public IList ItemsSource
	{
		get
		{
			return (IList)GetValue(ItemsSourceProperty);
		}
		set
		{
			SetValue(ItemsSourceProperty, value);
		}
	}

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

	public Type ItemType
	{
		get
		{
			return (Type)GetValue(ItemTypeProperty);
		}
		set
		{
			SetValue(ItemTypeProperty, value);
		}
	}

	public string Text
	{
		get
		{
			return (string)GetValue(TextProperty);
		}
		set
		{
			SetValue(TextProperty, value);
		}
	}

	private static void OnIsOpenChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PrimitiveTypeCollectionControl primitiveTypeCollectionControl)
		{
			primitiveTypeCollectionControl.OnIsOpenChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnIsOpenChanged(bool oldValue, bool newValue)
	{
	}

	private static void OnItemsSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PrimitiveTypeCollectionControl primitiveTypeCollectionControl)
		{
			primitiveTypeCollectionControl.OnItemsSourceChanged((IList)e.OldValue, (IList)e.NewValue);
		}
	}

	protected virtual void OnItemsSourceChanged(IList oldValue, IList newValue)
	{
		if (newValue != null)
		{
			if (ItemsSourceType == null)
			{
				ItemsSourceType = newValue.GetType();
			}
			if (ItemType == null && newValue.GetType().ContainsGenericParameters)
			{
				ItemType = newValue.GetType().GetGenericArguments()[0];
			}
			SetText(newValue);
		}
	}

	private static void OnTextChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PrimitiveTypeCollectionControl primitiveTypeCollectionControl)
		{
			primitiveTypeCollectionControl.OnTextChanged((string)e.OldValue, (string)e.NewValue);
		}
	}

	protected virtual void OnTextChanged(string oldValue, string newValue)
	{
		if (!_surpressTextChanged)
		{
			PersistChanges();
		}
	}

	static PrimitiveTypeCollectionControl()
	{
		IsOpenProperty = DependencyProperty.Register("IsOpen", typeof(bool), typeof(PrimitiveTypeCollectionControl), new UIPropertyMetadata(false, OnIsOpenChanged));
		ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IList), typeof(PrimitiveTypeCollectionControl), new UIPropertyMetadata(null, OnItemsSourceChanged));
		IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(PrimitiveTypeCollectionControl), new UIPropertyMetadata(false));
		ItemsSourceTypeProperty = DependencyProperty.Register("ItemsSourceType", typeof(Type), typeof(PrimitiveTypeCollectionControl), new UIPropertyMetadata(null));
		ItemTypeProperty = DependencyProperty.Register("ItemType", typeof(Type), typeof(PrimitiveTypeCollectionControl), new UIPropertyMetadata(null));
		TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(PrimitiveTypeCollectionControl), new UIPropertyMetadata(null, OnTextChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PrimitiveTypeCollectionControl), new FrameworkPropertyMetadata(typeof(PrimitiveTypeCollectionControl)));
	}

	private void PersistChanges()
	{
		IList list = ComputeItemsSource();
		if (list == null)
		{
			return;
		}
		IList list2 = ComputeItems();
		list.Clear();
		foreach (object item in list2)
		{
			list.Add(item);
		}
		if (_conversionFailed)
		{
			SetText(list);
		}
	}

	private IList ComputeItems()
	{
		IList list = new List<object>();
		if (ItemType == null)
		{
			return list;
		}
		string[] array = Text.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string value = array[i].TrimEnd('\r');
			if (!string.IsNullOrEmpty(value))
			{
				object obj = null;
				try
				{
					obj = ((!ItemType.IsEnum) ? Convert.ChangeType(value, ItemType) : Enum.Parse(ItemType, value));
				}
				catch
				{
					_conversionFailed = true;
				}
				if (obj != null)
				{
					list.Add(obj);
				}
			}
		}
		return list;
	}

	private IList ComputeItemsSource()
	{
		if (ItemsSource == null)
		{
			string text = Text;
			ItemsSource = CreateItemsSource();
			Text = text;
		}
		return ItemsSource;
	}

	private IList CreateItemsSource()
	{
		IList result = null;
		if (ItemsSourceType != null)
		{
			result = (IList)ItemsSourceType.GetConstructor(Type.EmptyTypes).Invoke(null);
		}
		return result;
	}

	private void SetText(IEnumerable collection)
	{
		_surpressTextChanged = true;
		StringBuilder stringBuilder = new StringBuilder();
		foreach (object item in collection)
		{
			stringBuilder.Append(item.ToString());
			stringBuilder.AppendLine();
		}
		Text = stringBuilder.ToString().Trim();
		_surpressTextChanged = false;
	}
}
