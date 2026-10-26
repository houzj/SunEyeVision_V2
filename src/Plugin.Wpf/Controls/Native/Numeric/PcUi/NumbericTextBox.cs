using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;

public class NumbericTextBox : TextBox
{
	public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register("MaxValue", typeof(double), typeof(NumbericTextBox), new PropertyMetadata(double.MaxValue));

	public static readonly DependencyProperty MinValueProperty = DependencyProperty.Register("MinValue", typeof(double), typeof(NumbericTextBox), new PropertyMetadata(double.MinValue));

	public static readonly DependencyProperty PrecisionProperty = DependencyProperty.Register("Precision", typeof(ushort), typeof(NumbericTextBox), new PropertyMetadata((ushort)2));

	private string lastLegalText;

	private bool isPaste;

	public double MaxValue
	{
		get
		{
			return (double)GetValue(MaxValueProperty);
		}
		set
		{
			SetValue(MaxValueProperty, value);
		}
	}

	public double MinValue
	{
		get
		{
			return (double)GetValue(MinValueProperty);
		}
		set
		{
			SetValue(MinValueProperty, value);
		}
	}

	public ushort Precision
	{
		get
		{
			return (ushort)GetValue(PrecisionProperty);
		}
		set
		{
			SetValue(PrecisionProperty, value);
		}
	}

	public event EventHandler<TextChangedEventArgs> PreviewTextChanged;

	public NumbericTextBox()
	{
		PreviewTextInput += NumbericTextBoxPreviewTextInput;
		TextChanged += NumbericTextBoxTextChanged;
		PreviewKeyDown += NumbericTextBox_PreviewKeyDown;
		LostFocus += NumbericTextBoxLostFocus;
		InputMethod.SetIsInputMethodEnabled(this, value: false);
		Loaded += NumbericTextBoxLoaded;
	}

	protected virtual void OnPreviewTextChanged(TextChangedEventArgs e)
	{
		if (PreviewTextChanged != null)
		{
			PreviewTextChanged(this, e);
		}
	}

	protected virtual void HandlePaste()
	{
		isPaste = false;
		bool flag = false;
		bool flag2 = false;
		double num = 1.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		string text = Text;
		foreach (char c in text)
		{
			if (!flag && c == '-')
			{
				num = -1.0;
				flag = true;
			}
			if (c >= '0' && c <= '9')
			{
				int num5 = c - 48;
				if (!flag2)
				{
					num2 = num2 * num + (double)num5;
					num = 10.0;
				}
				else
				{
					num /= 10.0;
					num2 += (double)num5 * num;
				}
				flag = true;
			}
			if (c == '.')
			{
				if (num4 + 1.0 > (double)(int)Precision)
				{
					break;
				}
				flag2 = true;
				flag = true;
				num = 0.1;
				num4++;
			}
			if (num2 < MinValue || num2 > MaxValue)
			{
				Text = num3.ToString(CultureInfo.InvariantCulture);
				SelectionStart = Text.Length;
				return;
			}
			num3 = num2;
		}
		Text = num2.ToString(CultureInfo.InvariantCulture);
		SelectionStart = Text.Length;
	}

	private void NumbericTextBoxLoaded(object sender, RoutedEventArgs e)
	{
		isPaste = true;
	}

	private void NumbericTextBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		isPaste = false;
		if (!short.TryParse(e.Text, out var _))
		{
			if (MinValue < 0.0 && e.Text == "-")
			{
				int num = Text.IndexOf('-');
				if (num == -1 && SelectionStart == 0)
				{
					return;
				}
			}
			if (Precision > 0 && e.Text == ".")
			{
				if (SelectionStart > Text.Length)
				{
					e.Handled = true;
					return;
				}
				int num2 = Text.IndexOf('.');
				if (num2 == -1 || (SelectionStart >= num2 && SelectionLength > 0))
				{
					return;
				}
			}
			e.Handled = true;
		}
		else
		{
			int num3 = Text.IndexOf('.');
			int selectionStart = SelectionStart;
			if (num3 != -1 && num3 < selectionStart && Text.Length - num3 > Precision && SelectionLength == 0)
			{
				e.Handled = true;
			}
		}
	}

	private void NumbericTextBoxTextChanged(object sender, TextChangedEventArgs e)
	{
		if (lastLegalText == Text)
		{
			return;
		}
		OnPreviewTextChanged(e);
		if (string.IsNullOrEmpty(Text))
		{
			return;
		}
		if (isPaste)
		{
			HandlePaste();
			lastLegalText = Text;
			return;
		}
		if (double.TryParse(Text, out var result))
		{
			int selectionStart = SelectionStart;
			if (result > MaxValue || result < MinValue)
			{
				Text = lastLegalText;
				SelectionStart = selectionStart;
				return;
			}
			lastLegalText = Text;
		}
		isPaste = true;
	}

	private void NumbericTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space)
		{
			e.Handled = true;
		}
	}

	private void NumbericTextBoxLostFocus(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrEmpty(Text))
		{
			Text = lastLegalText;
		}
	}
}
