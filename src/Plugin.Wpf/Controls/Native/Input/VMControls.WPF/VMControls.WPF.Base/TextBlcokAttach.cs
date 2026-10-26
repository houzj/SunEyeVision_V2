using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Base;

public class TextBlcokAttach
{
	public static readonly DependencyProperty AutoHeightProperty = DependencyProperty.RegisterAttached("AutoHeight", typeof(bool), typeof(TextBlcokAttach), new PropertyMetadata(false, OnChanged));

	public static bool GetAutoHeight(DependencyObject obj)
	{
		return (bool)obj.GetValue(AutoHeightProperty);
	}

	public static void SetAutoHeight(DependencyObject obj, bool value)
	{
		obj.SetValue(AutoHeightProperty, value);
	}

	private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is TextBlock textBlock && textBlock != null)
		{
			textBlock.Loaded += TextBox_Loaded;
			textBlock.Unloaded += TextBox_Unloaded;
		}
	}

	private static void TextBox_Loaded(object sender, RoutedEventArgs e)
	{
		TextBlock textBox = sender as TextBlock;
		ReSize(textBox);
	}

	private static void TextBox_Unloaded(object sender, RoutedEventArgs e)
	{
		TextBlock textBlock = sender as TextBlock;
	}

	private static void ReSize(TextBlock textBox)
	{
		int num = 0;
		double actualHeight = textBox.ActualHeight;
		double num2 = (double)textBox.GetValue(TextBlock.LineHeightProperty);
		if (actualHeight / num2 > 2.0)
		{
			num = 2;
			textBox.Height = num2 * (double)num;
		}
	}
}
