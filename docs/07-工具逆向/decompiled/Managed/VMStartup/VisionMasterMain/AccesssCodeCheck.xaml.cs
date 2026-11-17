using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using VM.Controls;

namespace VisionMasterMain;

public partial class AccesssCodeCheck : BaseWindow, IComponentConnector
{
	private bool ifShowPassword;

	private string _accessAdministatorCode = string.Empty;

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
					textBoxCodeSet.Text = textCodeSet.Password;
					textBoxCodeSet.Visibility = Visibility.Visible;
					textCodeSet.Visibility = Visibility.Collapsed;
				}
				else
				{
					textCodeSet.Password = textBoxCodeSet.Text;
					textBoxCodeSet.Visibility = Visibility.Collapsed;
					textCodeSet.Visibility = Visibility.Visible;
				}
				ifShowPassword = value;
			}
		}
	}

	public string AccessAdministatorCode
	{
		get
		{
			return _accessAdministatorCode;
		}
		set
		{
			_accessAdministatorCode = value;
		}
	}

	public AccesssCodeCheck()
	{
		InitializeComponent();
	}

	private void ApplyButtonClick(object sender, RoutedEventArgs e)
	{
		if (_accessAdministatorCode != textCodeSet.Password)
		{
			txtCodeErrorTips.Visibility = Visibility.Visible;
			txtCodeInfoTips.Visibility = Visibility.Collapsed;
		}
		else
		{
			DialogResult = true;
		}
	}

	public void ShowInfoTips()
	{
		txtCodeErrorTips.Visibility = Visibility.Collapsed;
		txtCodeInfoTips.Visibility = Visibility.Visible;
	}

	private void tb_PreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		Regex regex = new Regex("^[0-9a-zA-Z]*$");
		e.Handled = !regex.IsMatch(e.Text);
	}

	private void text_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space)
		{
			e.Handled = true;
		}
	}
}
