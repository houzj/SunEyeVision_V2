using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class CollectionControlButton : Button
{
	public static readonly DependencyProperty IsReadOnlyProperty;

	public static readonly DependencyProperty ItemsSourceProperty;

	public static readonly DependencyProperty ItemsSourceTypeProperty;

	public static readonly DependencyProperty NewItemTypesProperty;

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

	static CollectionControlButton()
	{
		IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(CollectionControlButton), new UIPropertyMetadata(false));
		ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(CollectionControlButton), new UIPropertyMetadata(null));
		ItemsSourceTypeProperty = DependencyProperty.Register("ItemsSourceType", typeof(Type), typeof(CollectionControlButton), new UIPropertyMetadata(null));
		NewItemTypesProperty = DependencyProperty.Register("NewItemTypes", typeof(IList), typeof(CollectionControlButton), new UIPropertyMetadata(null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CollectionControlButton), new FrameworkPropertyMetadata(typeof(CollectionControlButton)));
	}

	public CollectionControlButton()
	{
		Click += CollectionControlButton_Click;
	}

	private void CollectionControlButton_Click(object sender, RoutedEventArgs e)
	{
		CollectionControlDialog collectionControlDialog = new CollectionControlDialog();
		collectionControlDialog.ItemsSource = ItemsSource;
		collectionControlDialog.NewItemTypes = NewItemTypes;
		collectionControlDialog.ItemsSourceType = ItemsSourceType;
		collectionControlDialog.IsReadOnly = IsReadOnly;
		collectionControlDialog.ShowDialog();
	}
}
