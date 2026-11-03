using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Coordinates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;

public static class ItemsControlExtension
{
	public static readonly DependencyProperty DataSourceProperty = DependencyProperty.RegisterAttached("DataSource", typeof(object), typeof(ItemsControlExtension), new UIPropertyMetadata());

	public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.RegisterAttached("ItemsSource", typeof(string), typeof(ItemsControlExtension), new UIPropertyMetadata(OnItemsSourcePropertyChanged));

	public static object GetDataSource(DependencyObject obj)
	{
		return obj.GetValue(DataSourceProperty);
	}

	public static void SetDataSource(DependencyObject obj, object value)
	{
		obj.SetValue(DataSourceProperty, value);
	}

	public static string GetItemsSource(DependencyObject obj)
	{
		return (string)obj.GetValue(ItemsSourceProperty);
	}

	public static void SetItemsSource(DependencyObject obj, string value)
	{
		obj.SetValue(ItemsSourceProperty, value);
	}

	private static void OnItemsSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue != null && !string.IsNullOrEmpty(e.NewValue.ToString()) && d is ItemsControl)
		{
			ItemsControl itemsControl = d as ItemsControl;
			Binding binding = new Binding();
			binding.RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(BaseWindow), 1);
			binding.Path = new PropertyPath("DataContext." + e.NewValue.ToString());
			itemsControl.SetBinding(ItemsControl.ItemsSourceProperty, binding);
		}
	}
}
