using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Localization;
using VM.Controls;
using VM.User;

namespace VisionMasterMain;

public partial class ReSetPassCode : BaseWindow, IComponentConnector
{
	private bool ifShowOldCode;

	private bool ifShowNewCode;

	private bool ifShowCheckCode;

	public CodeManager myCodeManager { get; set; }

	public bool IfShowOldCode
	{
		get
		{
			return ifShowOldCode;
		}
		set
		{
			if (ifShowOldCode != value)
			{
				if (value)
				{
					textBoxOldCode.Text = adminOldCode.Password;
					textBoxOldCode.Visibility = Visibility.Visible;
					adminOldCode.Visibility = Visibility.Collapsed;
				}
				else
				{
					adminOldCode.Password = textBoxOldCode.Text;
					textBoxOldCode.Visibility = Visibility.Collapsed;
					adminOldCode.Visibility = Visibility.Visible;
				}
				ifShowOldCode = value;
			}
		}
	}

	public bool IfShowNewCode
	{
		get
		{
			return ifShowNewCode;
		}
		set
		{
			if (ifShowNewCode != value)
			{
				if (value)
				{
					textBoxNewCode.Text = adminNewCode.Password;
					textBoxNewCode.Visibility = Visibility.Visible;
					adminNewCode.Visibility = Visibility.Collapsed;
				}
				else
				{
					adminNewCode.Password = textBoxNewCode.Text;
					textBoxNewCode.Visibility = Visibility.Collapsed;
					adminNewCode.Visibility = Visibility.Visible;
				}
				ifShowNewCode = value;
			}
		}
	}

	public bool IfShowCheckCode
	{
		get
		{
			return ifShowCheckCode;
		}
		set
		{
			if (ifShowCheckCode != value)
			{
				if (value)
				{
					textBoxCheckCode.Text = adminCheckCode.Password;
					textBoxCheckCode.Visibility = Visibility.Visible;
					adminCheckCode.Visibility = Visibility.Collapsed;
				}
				else
				{
					adminCheckCode.Password = textBoxCheckCode.Text;
					textBoxCheckCode.Visibility = Visibility.Collapsed;
					adminCheckCode.Visibility = Visibility.Visible;
				}
				ifShowCheckCode = value;
			}
		}
	}

	public ReSetPassCode()
	{
		InitializeComponent();
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

	private void Apply_OnClick(object sender, RoutedEventArgs e)
	{
		ResetTipsVisible();
		if (CheckAminCodeModify())
		{
			if (!CodeManager.CheckCodeFormat(adminOldCode.Password))
			{
				oldCodeTips.Visibility = Visibility.Visible;
				return;
			}
			bool bEnable = false;
			string adminCode = "";
			myCodeManager.GetAdminCode(out bEnable, out adminCode);
			if (adminOldCode.Password != adminCode)
			{
				oldCodeTips.Visibility = Visibility.Visible;
				return;
			}
			if (!CodeManager.CheckCodeFormat(adminNewCode.Password))
			{
				newCodeTips.Visibility = Visibility.Visible;
				return;
			}
			if (adminNewCode.Password != adminCheckCode.Password)
			{
				checkCodeTips.Visibility = Visibility.Visible;
				return;
			}
			if (CheckAminCodeModify())
			{
				myCodeManager.SetAdminEnable(bEnable: true, adminNewCode.Password);
			}
			DialogResult = true;
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SetSuc"));
		}
		else
		{
			oldCodeTips.Visibility = Visibility.Visible;
		}
	}

	private bool CheckAminCodeModify()
	{
		if (adminNewCode.Password.Length > 0 || adminCheckCode.Password.Length > 0 || adminOldCode.Password.Length > 0)
		{
			return true;
		}
		return false;
	}

	private void ResetTipsVisible()
	{
		oldCodeTips.Visibility = Visibility.Collapsed;
		newCodeTips.Visibility = Visibility.Collapsed;
		checkCodeTips.Visibility = Visibility.Collapsed;
	}

	public static bool CheckCodeFormat(string tempCode)
	{
		if (tempCode.Length < 3 || tempCode.Length > 8)
		{
			return false;
		}
		return true;
	}

	private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
		e.Handled = true;
	}
}
