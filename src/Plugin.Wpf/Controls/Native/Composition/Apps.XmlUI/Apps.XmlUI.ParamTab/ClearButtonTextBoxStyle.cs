using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

public class ClearButtonTextBoxStyle
{
	public static readonly DependencyProperty IsClearButtonProperty = DependencyProperty.RegisterAttached("IsClearButton", typeof(bool), typeof(ClearButtonTextBoxStyle), new PropertyMetadata(false, ClearText));

	public static bool GetIsClearButton(DependencyObject obj)
	{
		return (bool)obj.GetValue(IsClearButtonProperty);
	}

	public static void SetIsClearButton(DependencyObject obj, bool value)
	{
		obj.SetValue(IsClearButtonProperty, value);
	}

	private static void ClearText(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		Button button = d as Button;
		if (d != null && e.OldValue != e.NewValue)
		{
			button.Click -= ClearTextClicked;
			if ((bool)e.NewValue)
			{
				button.Click += ClearTextClicked;
			}
		}
	}

	public static void ClearTextClicked(object sender, RoutedEventArgs e)
	{
		if (sender is Button reference)
		{
			DependencyObject parent = VisualTreeHelper.GetParent(reference);
			while (!(parent is TextBox))
			{
				parent = VisualTreeHelper.GetParent(parent);
			}
			if (parent is TextBox textBox)
			{
				textBox.Clear();
			}
		}
	}
}
