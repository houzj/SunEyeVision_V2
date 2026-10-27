using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView;

public partial class ColorPickerControlView : UserControl, IComponentConnector
{
	public static DependencyProperty CurrentColorProperty = DependencyProperty.Register("CurrentColor", typeof(SolidColorBrush), typeof(ColorPickerControlView), new PropertyMetadata(Brushes.Black));

	public static DependencyProperty TextVisibilityProperty = DependencyProperty.Register("TextVisibility", typeof(Visibility), typeof(ColorPickerControlView), new PropertyMetadata(Visibility.Visible));

	public static RoutedUICommand SelectColorCommand = new RoutedUICommand("SelectColorCommand", "SelectColorCommand", typeof(ColorPickerControlView));

	public SolidColorBrush CurrentColor
	{
		get
		{
			return (SolidColorBrush)GetValue(CurrentColorProperty);
		}
		set
		{
			SetValue(CurrentColorProperty, value);
		}
	}

	public Visibility TextVisibility
	{
		get
		{
			return (Visibility)GetValue(TextVisibilityProperty);
		}
		set
		{
			SetValue(TextVisibilityProperty, value);
		}
	}

	public object FirstItem => new ColorPickerFirstItem();

	public object SecondItem => new ColorPickerSecondItem();

	public ColorPickerControlView()
	{
		InitializeComponent();
		CommandBindings.Add(new CommandBinding(SelectColorCommand, SelectColorCommandExecute));
	}

	private void SelectColorCommandExecute(object sender, ExecutedRoutedEventArgs e)
	{
		CurrentColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(e.Parameter.ToString()));
	}

}
