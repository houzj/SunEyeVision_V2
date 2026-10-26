using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

public partial class LeftRightControl : Grid, IComponentConnector
{
	public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register("LeftContent", typeof(object), typeof(LeftRightControl), new PropertyMetadata(null, OnLeftContentChanged));

	public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register("RightContent", typeof(object), typeof(LeftRightControl), new PropertyMetadata(null, OnRightContentChanged));

	public static readonly DependencyProperty ChildLeftWidthProperty = DependencyProperty.Register("ChildLeftWidth", typeof(double), typeof(LeftRightControl), new PropertyMetadata(89.0));

	public static readonly DependencyProperty IsDisplayLeftProperty = DependencyProperty.Register("IsDisplayLeft", typeof(bool), typeof(LeftRightControl), new PropertyMetadata(true));

	public object LeftContent
	{
		get
		{
			return GetValue(LeftContentProperty);
		}
		set
		{
			SetValue(LeftContentProperty, value);
		}
	}

	public object RightContent
	{
		get
		{
			return GetValue(RightContentProperty);
		}
		set
		{
			SetValue(RightContentProperty, value);
		}
	}

	public double ChildLeftWidth
	{
		get
		{
			return (double)GetValue(ChildLeftWidthProperty);
		}
		set
		{
			SetValue(ChildLeftWidthProperty, value);
		}
	}

	public bool IsDisplayLeft
	{
		get
		{
			return (bool)GetValue(IsDisplayLeftProperty);
		}
		set
		{
			SetValue(IsDisplayLeftProperty, value);
		}
	}

	private static void OnLeftContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		UIElementCollection children = (d as LeftRightControl).Children;
		UIElement uIElement = e.NewValue as UIElement;
		children.Remove(e.OldValue as UIElement);
		children.Remove((d as LeftRightControl).PartText);
		if (uIElement != null)
		{
			children.Insert(0, uIElement);
			uIElement.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF666666")));
			uIElement.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		}
	}

	private static void OnRightContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		UIElementCollection children = (d as LeftRightControl).Children;
		UIElement uIElement = e.NewValue as UIElement;
		children.Remove(e.OldValue as UIElement);
		if (uIElement != null)
		{
			children.Add(uIElement);
			uIElement.SetValue(Grid.ColumnProperty, 1);
			uIElement.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
		}
	}

	public LeftRightControl()
	{
		InitializeComponent();
	}
}
