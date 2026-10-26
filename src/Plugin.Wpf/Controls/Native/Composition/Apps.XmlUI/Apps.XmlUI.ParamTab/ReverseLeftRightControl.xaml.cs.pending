using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

public partial class ReverseLeftRightControl : Grid, IComponentConnector
{
	public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register("LeftContent", typeof(object), typeof(ReverseLeftRightControl), new PropertyMetadata(null, OnLeftContentChanged));

	public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register("RightContent", typeof(object), typeof(ReverseLeftRightControl), new PropertyMetadata(null, OnRightContentChanged));

	public static readonly DependencyProperty ChildLeftWidthProperty = DependencyProperty.Register("ChildLeftWidth", typeof(double), typeof(ReverseLeftRightControl), new PropertyMetadata(89.0));

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

	private static void OnLeftContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		UIElementCollection children = (d as ReverseLeftRightControl).Children;
		UIElement uIElement = e.NewValue as UIElement;
		children.Remove(e.OldValue as UIElement);
		if (uIElement != null)
		{
			children.Insert(0, uIElement);
			uIElement.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF666666")));
			uIElement.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		}
	}

	private static void OnRightContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		UIElementCollection children = (d as ReverseLeftRightControl).Children;
		UIElement uIElement = e.NewValue as UIElement;
		children.Remove(e.OldValue as UIElement);
		children.Remove((d as ReverseLeftRightControl).PartText);
		if (uIElement != null)
		{
			children.Add(uIElement);
			uIElement.SetValue(Grid.ColumnProperty, 1);
			uIElement.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
		}
	}

	public ReverseLeftRightControl()
	{
		InitializeComponent();
	}
}
