using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using VM.Controls;
using VM.User;

namespace VisionMaster;

public partial class AccessManagerWin : BaseWindow, IComponentConnector
{
	private bool ifShowOldCode;

	private bool ifShowNewCode;

	private bool ifShowCheckCode;

	private bool ifShowTechSetCode;

	private bool ifShowOperatorSetCode;

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

	public bool IfShowTechSetCode
	{
		get
		{
			return ifShowTechSetCode;
		}
		set
		{
			if (ifShowTechSetCode != value)
			{
				if (value)
				{
					techCode.Text = techSetCode.Password;
					techCode.Visibility = Visibility.Visible;
					techSetCode.Visibility = Visibility.Collapsed;
				}
				else
				{
					techSetCode.Password = techCode.Text;
					techCode.Visibility = Visibility.Collapsed;
					techSetCode.Visibility = Visibility.Visible;
				}
				ifShowTechSetCode = value;
			}
		}
	}

	public bool IfShowOperatorSetCode
	{
		get
		{
			return ifShowOperatorSetCode;
		}
		set
		{
			if (ifShowOperatorSetCode != value)
			{
				if (value)
				{
					operatorCode.Text = operatorSetCode.Password;
					operatorCode.Visibility = Visibility.Visible;
					operatorSetCode.Visibility = Visibility.Collapsed;
				}
				else
				{
					operatorSetCode.Password = operatorCode.Text;
					operatorCode.Visibility = Visibility.Collapsed;
					operatorSetCode.Visibility = Visibility.Visible;
				}
				ifShowOperatorSetCode = value;
			}
		}
	}

	public CodeManager myCodeManager { get; set; }

	public AccessManagerWin()
	{
		InitializeComponent();
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
		teckCodeTips.Visibility = Visibility.Collapsed;
		operatorCodeTips.Visibility = Visibility.Collapsed;
	}

	public bool InitUserStatus()
	{
		if (myCodeManager == null)
		{
			return false;
		}
		bool bEnable = false;
		string text = "";
		if (AccessUser.Tech == myCodeManager.GetLoginUser())
		{
			panelAdminTech.Visibility = Visibility.Collapsed;
		}
		myCodeManager.GetTechCode(out bEnable, out text);
		if (!bEnable)
		{
			techEnable.IsChecked = bEnable;
			techCode.IsEnabled = false;
			techCode.Text = "";
			techSetCode.Password = "";
		}
		else
		{
			techEnable.IsChecked = bEnable;
			techCode.Text = text;
			techSetCode.Password = text;
		}
		myCodeManager.GetOperatorCode(out bEnable, out text);
		if (!bEnable)
		{
			operatorEnable.IsChecked = bEnable;
			operatorCode.IsEnabled = false;
			operatorCode.Text = "";
			operatorSetCode.Password = "";
		}
		else
		{
			operatorEnable.IsChecked = bEnable;
			operatorCode.Text = text;
			operatorSetCode.Password = text;
		}
		return true;
	}

	private void ButtonApply_Click(object sender, RoutedEventArgs e)
	{
		ResetTipsVisible();
		Regex regex = new Regex("^[0-9a-zA-Z]*$");
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
		}
		bool? isChecked = techEnable.IsChecked;
		bool flag = true;
		if (isChecked == true == flag && isChecked.HasValue && !CodeManager.CheckCodeFormat(techSetCode.Password))
		{
			teckCodeTips.Visibility = Visibility.Visible;
			return;
		}
		isChecked = operatorEnable.IsChecked;
		flag = true;
		if (isChecked == true == flag && isChecked.HasValue && !CodeManager.CheckCodeFormat(operatorSetCode.Password))
		{
			operatorCodeTips.Visibility = Visibility.Visible;
			return;
		}
		if (CheckAminCodeModify())
		{
			myCodeManager.SetAdminEnable(bEnable: true, adminNewCode.Password);
		}
		if (!techEnable.IsChecked.HasValue)
		{
			techEnable.IsChecked = false;
		}
		myCodeManager.SetTechEnable(techEnable.IsChecked.Value, techSetCode.Password);
		if (!operatorEnable.IsChecked.HasValue)
		{
			operatorEnable.IsChecked = false;
		}
		myCodeManager.SetOperatorEnable(operatorEnable.IsChecked.Value, operatorSetCode.Password);
		DialogResult = true;
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

	private void techEnable_Checked(object sender, RoutedEventArgs e)
	{
		techCode.IsEnabled = true;
	}

	private void techEnable_Unchecked(object sender, RoutedEventArgs e)
	{
		techCode.IsEnabled = false;
		techCode.Text = "";
		techSetCode.Password = "";
	}

	private void operatorEnable_Checked(object sender, RoutedEventArgs e)
	{
		operatorCode.IsEnabled = true;
	}

	private void operatorEnable_Unchecked(object sender, RoutedEventArgs e)
	{
		operatorCode.IsEnabled = false;
		operatorCode.Text = "";
		operatorSetCode.Password = "";
	}
}
