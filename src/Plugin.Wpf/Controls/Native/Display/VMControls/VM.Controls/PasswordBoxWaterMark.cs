using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class PasswordBoxWaterMark
{
	public static readonly DependencyProperty IsMonitoringProperty = DependencyProperty.RegisterAttached("IsMonitoring", typeof(bool), typeof(PasswordBoxWaterMark), new UIPropertyMetadata(false, OnIsMonitoringChanged));

	public static readonly DependencyProperty PasswordLengthProperty = DependencyProperty.RegisterAttached("PasswordLength", typeof(int), typeof(PasswordBoxWaterMark), new UIPropertyMetadata(0));

	public static readonly DependencyProperty PasswordProperty = DependencyProperty.RegisterAttached("Password", typeof(string), typeof(PasswordBoxWaterMark), new PropertyMetadata("", PasswordPropertyChangedCallback));

	public static bool GetIsMonitoring(DependencyObject obj)
	{
		return (bool)obj.GetValue(IsMonitoringProperty);
	}

	public static void SetIsMonitoring(DependencyObject obj, bool value)
	{
		obj.SetValue(IsMonitoringProperty, value);
	}

	public static int GetPasswordLength(DependencyObject obj)
	{
		return (int)obj.GetValue(PasswordLengthProperty);
	}

	public static void SetPasswordLength(DependencyObject obj, int value)
	{
		obj.SetValue(PasswordLengthProperty, value);
	}

	private static void OnIsMonitoringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is PasswordBox passwordBox)
		{
			if ((bool)e.NewValue)
			{
				passwordBox.PasswordChanged += PasswordChanged;
				SetPasswordLength(passwordBox, passwordBox.Password.Length);
			}
			else
			{
				passwordBox.PasswordChanged -= PasswordChanged;
			}
		}
	}

	private static void PasswordChanged(object sender, RoutedEventArgs e)
	{
		if (sender is PasswordBox passwordBox)
		{
			SetPasswordLength(passwordBox, passwordBox.Password.Length);
		}
	}

	public static string GetPassword(DependencyObject obj)
	{
		return (string)obj.GetValue(PasswordProperty);
	}

	public static void SetPassword(DependencyObject obj, string value)
	{
		obj.SetValue(PasswordProperty, value);
	}

	private static void PasswordPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is PasswordBox passwordBox)
		{
			passwordBox.Password = e.NewValue.ToString();
			passwordBox.GetType().GetMethod("Select", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(passwordBox, new object[2]
			{
				passwordBox.Password.Length,
				0
			});
		}
	}
}
