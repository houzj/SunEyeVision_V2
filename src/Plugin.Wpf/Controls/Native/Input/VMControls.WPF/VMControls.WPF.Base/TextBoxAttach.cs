using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Base;

public class TextBoxAttach
{
	public static readonly DependencyProperty AutoHeightProperty = DependencyProperty.RegisterAttached("AutoHeight", typeof(bool), typeof(TextBoxAttach), new PropertyMetadata(false, OnChanged));

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
		if (d is TextBox textBox && textBox != null)
		{
			textBox.Loaded += TextBox_Loaded;
			textBox.Unloaded += TextBox_Unloaded;
			textBox.TextChanged += TextBox_TextChanged;
			textBox.SizeChanged += TextBox_SizeChanged;
		}
	}

	private static void TextBox_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		TextBox textBox = sender as TextBox;
		if (textBox.IsLoaded)
		{
			ReSize(textBox);
		}
	}

	private static void TextBox_Unloaded(object sender, RoutedEventArgs e)
	{
		TextBox textBox = sender as TextBox;
		textBox.Unloaded -= TextBox_Unloaded;
		textBox.Loaded -= TextBox_Loaded;
		textBox.TextChanged -= TextBox_TextChanged;
	}

	private static void TextBox_Loaded(object sender, RoutedEventArgs e)
	{
		TextBox textBox = sender as TextBox;
		ReSize(textBox);
	}

	private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		TextBox textBox = sender as TextBox;
		if (textBox.IsLoaded)
		{
			ReSize(textBox);
		}
	}

	private static void ReSize(TextBox textBox)
	{
		int num = textBox.LineCount;
		if (num != -1)
		{
			double num2 = (double)textBox.GetValue(TextBlock.LineHeightProperty);
			if (num > 2)
			{
				num = 2;
			}
			double height = num2 * (double)num;
			textBox.Height = height;
		}
	}
}
