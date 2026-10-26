using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

public partial class ResultOverviewPopupButton : UserControl, IComponentConnector
{
	public static readonly DependencyProperty ButtonStyleProperty = DependencyProperty.Register("ButtonStyle", typeof(Style), typeof(ResultOverviewPopupButton), new PropertyMetadata(null));

	public static readonly DependencyProperty SubContentProperty = DependencyProperty.Register("SubContent", typeof(ContentControl), typeof(ResultOverviewPopupButton), new PropertyMetadata(null));

	public static readonly DependencyProperty HorizontalOffsetValProperty = DependencyProperty.Register("HorizontalOffsetVal", typeof(double), typeof(ResultOverviewPopupButton), new PropertyMetadata(null));

	public static readonly DependencyProperty VerticalOffsetValProperty = DependencyProperty.Register("VerticalOffsetVal", typeof(double), typeof(ResultOverviewPopupButton), new PropertyMetadata(null));

	public Style ButtonStyle
	{
		get
		{
			return (Style)GetValue(ButtonStyleProperty);
		}
		set
		{
			SetValue(ButtonStyleProperty, value);
		}
	}

	public ContentControl SubContent
	{
		get
		{
			return (ContentControl)GetValue(SubContentProperty);
		}
		set
		{
			SetValue(SubContentProperty, value);
		}
	}

	public double HorizontalOffsetVal
	{
		get
		{
			return (double)GetValue(HorizontalOffsetValProperty);
		}
		set
		{
			SetValue(HorizontalOffsetValProperty, value);
		}
	}

	public double VerticalOffsetVal
	{
		get
		{
			return (double)GetValue(VerticalOffsetValProperty);
		}
		set
		{
			SetValue(VerticalOffsetValProperty, value);
		}
	}

	public ResultOverviewPopupButton()
	{
		InitializeComponent();
	}
}
