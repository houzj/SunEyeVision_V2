using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Apps.Localization;
using VM.Controls;

namespace VisionMasterMain.AppMainWindow.SoftwareSet;

public partial class ReSetPassCommonWindow : BaseWindow, IComponentConnector
{
	private Visibility _progressBarVisibility = Visibility.Hidden;

	private string _passwordTips = "";

	public Visibility ProgressBarVisibility
	{
		get
		{
			return _progressBarVisibility;
		}
		set
		{
			SetPropertyChanged(ref _progressBarVisibility, value, "ProgressBarVisibility");
			if (value == Visibility.Visible)
			{
				RspCommonWindow.IsEnabled = false;
			}
			else
			{
				RspCommonWindow.IsEnabled = true;
			}
		}
	}

	public string PasswordTips
	{
		get
		{
			return _passwordTips;
		}
		set
		{
			SetPropertyChanged(ref _passwordTips, value, "PasswordTips");
		}
	}

	public event Func<string, string, Task<bool>> ConfirmActor;

	public ReSetPassCommonWindow()
	{
		InitializeComponent();
	}

	private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
	{
		if (sender is PasswordBox passwordBox)
		{
			PasswordBoxWaterMark.SetPassword(passwordBox, passwordBox.Password);
		}
	}

	private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			ConfirmButton.IsEnabled = false;
			if (string.IsNullOrEmpty(OldPasswordBox.Password))
			{
				PasswordTips = AppLocalizationService.GetLang("Lang_OldPasswordTips");
				return;
			}
			if (string.IsNullOrEmpty(NewPasswordBox.Password))
			{
				PasswordTips = AppLocalizationService.GetLang("Lang_NewPasswordTips");
				return;
			}
			if (string.IsNullOrEmpty(ConfirmPasswordBox.Password))
			{
				PasswordTips = AppLocalizationService.GetLang("Lang_PasswordConfirmTips");
				return;
			}
			if (NewPasswordBox.Password != ConfirmPasswordBox.Password)
			{
				PasswordTips = AppLocalizationService.GetLang("两次密码不一致");
				return;
			}
			Regex regular = new Regex("^(?![0-9]+$)(?![a-zA-Z]+$)[0-9A-Za-z]{8,16}$");
			if (!regular.IsMatch(NewPasswordBox.Password))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PasswordFailed"), MessageIcons.Warning);
				return;
			}
			ProgressBarVisibility = Visibility.Visible;
			PasswordTips = "";
			if (await (ConfirmActor?.Invoke(OldPasswordBox.Password, NewPasswordBox.Password)))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PasswordModifiedSuccessfully"), MessageIcons.Ok);
				DialogResult = true;
			}
			else
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PasswordModifiedFailed"), MessageIcons.Warning);
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			ProgressBarVisibility = Visibility.Hidden;
			HikMessageBox.Show(ex2.Message, MessageIcons.Error);
		}
		finally
		{
			ConfirmButton.IsEnabled = true;
			ProgressBarVisibility = Visibility.Hidden;
		}
	}
}
