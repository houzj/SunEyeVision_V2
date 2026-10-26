using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

public class TrimmedTextBlock : TextBlock
{
	public static readonly DependencyProperty IsTextTrimmedProperty = DependencyProperty.Register("IsTextTrimmed", typeof(bool), typeof(TrimmedTextBlock), new PropertyMetadata(false, OnIsTextTrimmedChanged));

	public bool IsTextTrimmed
	{
		get
		{
			return (bool)GetValue(IsTextTrimmedProperty);
		}
		private set
		{
			SetValue(IsTextTrimmedProperty, value);
		}
	}

	public TrimmedTextBlock()
	{
		SizeChanged += TrimmedTextBlock_SizeChanged;
	}

	private static void OnIsTextTrimmedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is TrimmedTextBlock trimmedTextBlock)
		{
			trimmedTextBlock.OnIsTextTrimmedChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	private void OnIsTextTrimmedChanged(bool oldValue, bool newValue)
	{
		ToolTip = (newValue ? Text : null);
	}

	private void TrimmedTextBlock_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (sender is TextBlock textBlock)
		{
			IsTextTrimmed = GetIsTextTrimmed(textBlock);
		}
	}

	private bool GetIsTextTrimmed(TextBlock textBlock)
	{
		if (textBlock == null)
		{
			return false;
		}
		if (textBlock.TextTrimming == TextTrimming.None)
		{
			return false;
		}
		if (textBlock.TextWrapping != TextWrapping.NoWrap)
		{
			return false;
		}
		double actualWidth = textBlock.ActualWidth;
		textBlock.Measure(new Size(double.MaxValue, double.MaxValue));
		double width = textBlock.DesiredSize.Width;
		return actualWidth < width;
	}
}
