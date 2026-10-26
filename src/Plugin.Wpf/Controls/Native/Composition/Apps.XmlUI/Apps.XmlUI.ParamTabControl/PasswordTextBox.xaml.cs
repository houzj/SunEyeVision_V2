using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;

public partial class PasswordTextBox : UserControl, IComponentConnector
{
	public delegate void PasswordChange(object sender, RoutedPropertyChangedEventArgs<double> e);

	private bool ifShowPassword = false;

	public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register("Password", typeof(string), typeof(PasswordTextBox), new PropertyMetadata("", PasswordChangeCallBack));

	public bool IfShowPassword
	{
		get
		{
			return ifShowPassword;
		}
		set
		{
			if (ifShowPassword != value)
			{
				if (value)
				{
					textBox.Text = passwordBox.Password;
					textBox.Visibility = Visibility.Visible;
					passwordBox.Visibility = Visibility.Collapsed;
				}
				else
				{
					passwordBox.Password = textBox.Text;
					textBox.Visibility = Visibility.Collapsed;
					passwordBox.Visibility = Visibility.Visible;
				}
				ifShowPassword = value;
			}
		}
	}

	public string Password
	{
		get
		{
			return (string)GetValue(PasswordProperty);
		}
		set
		{
			SetValue(PasswordProperty, value);
		}
	}

	public event PasswordChange OnPasswordChange;

	public PasswordTextBox()
	{
		InitializeComponent();
	}

	private void text_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space)
		{
			e.Handled = true;
		}
	}

	private void text_LostFocus(object sender, RoutedEventArgs e)
	{
		string password = "";
		if (sender is PasswordBox passwordBox)
		{
			password = passwordBox.Password;
		}
		else if (sender is TextBox textBox)
		{
			password = textBox.Text;
		}
		Password = password;
		if (OnPasswordChange != null)
		{
			OnPasswordChange(this, null);
		}
	}

	private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
	{
		e.Handled = false;
	}

	private static void PasswordChangeCallBack(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as PasswordTextBox).DoPasswordChangeCallBack();
	}

	private void DoPasswordChangeCallBack()
	{
		passwordBox.Password = Password;
		textBox.Text = Password;
	}
}
