using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Localization;
using VM.Controls;
using VM.User;

namespace VisionMasterMain;

public partial class AccessCodeSet : BaseWindow, IComponentConnector
{
	private bool ifShowPassword;

	private bool ifShowPasswordRepeat;

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

	public bool IfShowPasswordRepeat
	{
		get
		{
			return ifShowPasswordRepeat;
		}
		set
		{
			if (ifShowPasswordRepeat != value)
			{
				if (value)
				{
					textBoxCodeCheck.Text = textCodeCheck.Password;
					textBoxCodeCheck.Visibility = Visibility.Visible;
					textCodeCheck.Visibility = Visibility.Collapsed;
				}
				else
				{
					textCodeCheck.Password = textBoxCodeCheck.Text;
					textBoxCodeCheck.Visibility = Visibility.Collapsed;
					textCodeCheck.Visibility = Visibility.Visible;
				}
				ifShowPasswordRepeat = value;
			}
		}
	}

	public string AccessAdministatorCode => _accessAdministatorCode;

	public AccessCodeSet()
	{
		InitializeComponent();
	}

	private void ApplyButtonClick(object sender, RoutedEventArgs e)
	{
		if (CheckCodeReasonable())
		{
			_accessAdministatorCode = textCodeSet.Password;
			DialogResult = true;
		}
	}

	private bool CheckCodeReasonable()
	{
		if (textCodeSet.Password != textCodeCheck.Password)
		{
			txtCodeErrorTips.Text = AppLocalizationService.GetLang("Lang_Err") + ":" + AppLocalizationService.GetLang("两次密码不一致");
			txtCodeErrorTips.Visibility = Visibility.Visible;
			return false;
		}
		if (!CodeManager.CheckCodeFormat(textCodeSet.Password))
		{
			txtCodeErrorTips.Text = AppLocalizationService.GetLang("Lang_Err") + "：" + AppLocalizationService.GetLang("请输入3~15位密码");
			txtCodeErrorTips.Visibility = Visibility.Visible;
			return false;
		}
		return true;
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

	private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
		e.Handled = true;
	}
}
