using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using VM.AppData;
using VM.User;
using VisionMasterMain.AccessManager;

namespace VisionMasterMain;

public partial class LoginWindow : Window, IComponentConnector
{
	private bool ifShowPassword;

	private bool _closing = false;

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
					textBoxUserCode.Text = userCode.Password;
					textBoxUserCode.Visibility = Visibility.Visible;
					userCode.Visibility = Visibility.Collapsed;
				}
				else
				{
					userCode.Password = textBoxUserCode.Text;
					textBoxUserCode.Visibility = Visibility.Collapsed;
					userCode.Visibility = Visibility.Visible;
				}
				ifShowPassword = value;
			}
		}
	}

	public LoginWindow()
	{
		InitializeComponent();
		AppData appData = AppDataFactory.Instance();
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.UriSource = new Uri(AppDomain.CurrentDomain.BaseDirectory + "Resources\\logo.png", UriKind.RelativeOrAbsolute);
		bitmapImage.DecodePixelWidth = 74;
		bitmapImage.DecodePixelHeight = 74;
		bitmapImage.EndInit();
		imgLogo.Source = bitmapImage;
		if (appData.IsHideLoginLogo)
		{
			imgLogo.Visibility = Visibility.Collapsed;
		}
	}

	private void Image_MouseMove(object sender, MouseEventArgs e)
	{
		if (e.LeftButton == MouseButtonState.Pressed)
		{
			DragMove();
		}
	}

	public bool SetUserStatus(bool isLoginMode = false)
	{
		bool bEnable = false;
		string adminCode = "";
		CodeManager.GetInstance().GetAdminCode(out bEnable, out adminCode);
		radioAmin.IsEnabled = bEnable;
		CodeManager.GetInstance().GetTechCode(out bEnable, out adminCode);
		radioTech.IsEnabled = bEnable;
		CodeManager.GetInstance().GetOperatorCode(out bEnable, out adminCode);
		radioOperator.IsEnabled = bEnable;
		switch (CodeManager.GetInstance().GetLoginUser())
		{
		case AccessUser.Admin:
			radioAmin.IsChecked = true;
			if (isLoginMode)
			{
				radioOperator.IsEnabled = false;
				radioTech.IsEnabled = false;
			}
			break;
		case AccessUser.Tech:
			radioTech.IsChecked = true;
			if (isLoginMode)
			{
				radioOperator.IsEnabled = false;
				radioAmin.IsEnabled = false;
			}
			break;
		case AccessUser.Operator:
			radioOperator.IsChecked = true;
			if (isLoginMode)
			{
				radioAmin.IsEnabled = false;
				radioTech.IsEnabled = false;
			}
			break;
		}
		return true;
	}

	private void ButtonCose_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
	}

	private AccessUser GetUser()
	{
		AccessUser accessUser = AccessUser.None;
		if (radioAmin.IsChecked.Value)
		{
			return AccessUser.Admin;
		}
		if (radioTech.IsChecked.Value)
		{
			return AccessUser.Tech;
		}
		if (radioOperator.IsChecked.Value)
		{
			return AccessUser.Operator;
		}
		return AccessUser.None;
	}

	private void ButtonLogin_Click(object sender, RoutedEventArgs e)
	{
		codeErrorTips.Visibility = Visibility.Collapsed;
		codeEmptyTips.Visibility = Visibility.Collapsed;
		if (string.IsNullOrEmpty(userCode.Password))
		{
			codeEmptyTips.Visibility = Visibility.Visible;
			return;
		}
		AccessUser user = GetUser();
		string adminCode = "";
		bool bEnable = false;
		switch (user)
		{
		case AccessUser.Admin:
			CodeManager.GetInstance().GetAdminCode(out bEnable, out adminCode);
			break;
		case AccessUser.Tech:
			CodeManager.GetInstance().GetTechCode(out bEnable, out adminCode);
			break;
		case AccessUser.Operator:
			CodeManager.GetInstance().GetOperatorCode(out bEnable, out adminCode);
			break;
		}
		if (userCode.Password != adminCode)
		{
			codeErrorTips.Visibility = Visibility.Visible;
			return;
		}
		AccessManagerHelper.SetLoginUser(user);
		if (!_closing)
		{
			DialogResult = true;
		}
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);
		if (!e.Cancel)
		{
			_closing = true;
		}
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

	private void RadioUser_Click(object sender, RoutedEventArgs e)
	{
		userCode.Password = "";
		textBoxUserCode.Text = "";
		codeErrorTips.Visibility = Visibility.Collapsed;
		codeEmptyTips.Visibility = Visibility.Collapsed;
	}

	private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
		e.Handled = true;
	}
}
