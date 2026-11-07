using System.Windows;

namespace VM.Controls;

public class WatermarkTextBox : AutoSelectTextBox
{
	public static readonly DependencyProperty KeepWatermarkOnGotFocusProperty;

	public static readonly DependencyProperty WatermarkProperty;

	public static readonly DependencyProperty WatermarkTemplateProperty;

	public bool KeepWatermarkOnGotFocus
	{
		get
		{
			return (bool)GetValue(KeepWatermarkOnGotFocusProperty);
		}
		set
		{
			SetValue(KeepWatermarkOnGotFocusProperty, value);
		}
	}

	public object Watermark
	{
		get
		{
			return GetValue(WatermarkProperty);
		}
		set
		{
			SetValue(WatermarkProperty, value);
		}
	}

	public DataTemplate WatermarkTemplate
	{
		get
		{
			return (DataTemplate)GetValue(WatermarkTemplateProperty);
		}
		set
		{
			SetValue(WatermarkTemplateProperty, value);
		}
	}

	static WatermarkTextBox()
	{
		KeepWatermarkOnGotFocusProperty = DependencyProperty.Register("KeepWatermarkOnGotFocus", typeof(bool), typeof(WatermarkTextBox), new UIPropertyMetadata(false));
		WatermarkProperty = DependencyProperty.Register("Watermark", typeof(object), typeof(WatermarkTextBox), new UIPropertyMetadata(null));
		WatermarkTemplateProperty = DependencyProperty.Register("WatermarkTemplate", typeof(DataTemplate), typeof(WatermarkTextBox), new UIPropertyMetadata(null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(WatermarkTextBox), new FrameworkPropertyMetadata(typeof(WatermarkTextBox)));
	}
}
