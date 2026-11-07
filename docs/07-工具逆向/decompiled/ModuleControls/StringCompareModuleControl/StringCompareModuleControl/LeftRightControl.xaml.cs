using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace StringCompareModuleControl;

public partial class LeftRightControl : Grid, IComponentConnector
{
	public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register("LeftContent", typeof(object), typeof(LeftRightControl), new PropertyMetadata(null));

	public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register("RightContent", typeof(object), typeof(LeftRightControl), new PropertyMetadata(null));

	public static readonly DependencyProperty ChildLeftWidthProperty = DependencyProperty.Register("ChildLeftWidth", typeof(double), typeof(LeftRightControl), new PropertyMetadata(89.0));

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

	public LeftRightControl()
	{
		InitializeComponent();
	}
}
