using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Localization;
using Apps.Log;
using Microsoft.Win32;
using VM.Controls;
using VM.User;
using VM.Utility;
using VisionMasterMain.AccessManager;
using starshipxac.Windows.Shell.Dialogs;

namespace VisionMasterMain;

public partial class AccessPage : Page, IComponentConnector
{
	private bool ifShowTechSetCode;

	private bool ifShowOperatorSetCode;

	private bool ADMIN = false;

	private bool TECH = false;

	private bool OPER = false;

	private bool IsChecked = false;

	private const string AccessCheckDiskKey = "AccessCheckDisk";

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

	public AccessPage()
	{
		InitializeComponent();
		ADMIN = false;
		InitInfo();
	}

	public void InitInfo()
	{
		myCodeManager = CodeManager.GetInstance();
		if (myCodeManager == null)
		{
			return;
		}
		bool bEnable = false;
		string text = "";
		if (AccessUser.Admin == myCodeManager.GetLoginUser())
		{
			autoAccessEnable.IsChecked = true;
			IsChecked = true;
			adminparam.Visibility = Visibility.Visible;
			techparam.Visibility = Visibility.Visible;
			operatparam.Visibility = Visibility.Visible;
			titleparam.Visibility = Visibility.Visible;
			myCodeManager.GetTechCode(out bEnable, out text);
			if (!bEnable)
			{
				techEnable.IsChecked = bEnable;
				techCode.IsEnabled = false;
				techCode.Text = "";
				techSetCode.Password = "";
				techAccessSet.IsEnabled = false;
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
		}
		else
		{
			adminparam.Visibility = Visibility.Collapsed;
			techparam.Visibility = Visibility.Collapsed;
			operatparam.Visibility = Visibility.Collapsed;
			titleparam.Visibility = Visibility.Visible;
			autoAccessEnable.IsChecked = false;
			IsChecked = false;
		}
		if (AccessUser.Tech == myCodeManager.GetLoginUser())
		{
			titleparam.Visibility = Visibility.Collapsed;
			operatparam.Visibility = Visibility.Visible;
			myCodeManager.GetTechCode(out bEnable, out text);
			if (!bEnable)
			{
				techEnable.IsChecked = bEnable;
				techCode.IsEnabled = false;
				techCode.Text = string.Empty;
				techSetCode.Password = string.Empty;
				techAccessSet.IsEnabled = false;
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
		}
		ADMIN = true;
	}

	private void autoAccessEnable_Click(object sender, RoutedEventArgs e)
	{
		if (IsChecked)
		{
			autoAccessEnable.IsChecked = true;
			if (CheckPassAdminAccess())
			{
				DialogResult dialogResult = HikMessageBox.Show(AppLocalizationService.GetLang("Lang_CancelAuthr"), AppLocalizationService.GetLang("Lang_AuthrCelAward"), MessageButtons.YesOrNo, MessageIcons.Warning, isTranslate: true);
				if (dialogResult == DialogResult.Yes)
				{
					autoAccessEnable.IsChecked = false;
					IsChecked = false;
					ClearXamlData();
					accessparam.Visibility = Visibility.Collapsed;
					CodeManager.GetInstance().ResetAccess();
				}
			}
			return;
		}
		autoAccessEnable.IsChecked = false;
		if (!ADMIN)
		{
			return;
		}
		AccessCodeSet accessCodeSet = new AccessCodeSet();
		if (false != accessCodeSet.ShowDialog())
		{
			DialogResult dialogResult2 = HikMessageBox.Show(AppLocalizationService.GetLang("Lang_StartAuthr"), AppLocalizationService.GetLang("Lang_ScrAward"), MessageButtons.YesOrNo, MessageIcons.Warning, isTranslate: true);
			if (dialogResult2 == DialogResult.Yes)
			{
				autoAccessEnable.IsChecked = true;
				IsChecked = true;
				ClearXamlData();
				accessparam.Visibility = Visibility.Visible;
				adminparam.Visibility = Visibility.Visible;
				techparam.Visibility = Visibility.Visible;
				operatparam.Visibility = Visibility.Visible;
				titleparam.Visibility = Visibility.Visible;
				CodeManager.GetInstance().SetAdminEnable(bEnable: true, accessCodeSet.AccessAdministatorCode);
				AccessManagerHelper.SetLoginUser(AccessUser.Admin);
			}
		}
	}

	private void ReSetPassword_Click(object sender, RoutedEventArgs e)
	{
		ReSetPassCode reSetPassCode = new ReSetPassCode
		{
			myCodeManager = CodeManager.GetInstance()
		};
		reSetPassCode.ShowDialog();
	}

	private void Permission_Click(object sender, RoutedEventArgs e)
	{
		AccessModuleManager accessModuleManager = new AccessModuleManager();
		accessModuleManager.ShowDialog();
	}

	private void ResetTipsVisible()
	{
		teckCodeTips.Visibility = Visibility.Collapsed;
		operatorCodeTips.Visibility = Visibility.Collapsed;
	}

	public bool AccessApply()
	{
		ResetTipsVisible();
		if (!CheckPassword())
		{
			return false;
		}
		SaveInfo();
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

	private bool CheckPassword()
	{
		bool? isChecked = techEnable.IsChecked;
		bool flag = true;
		if (isChecked == true == flag && isChecked.HasValue && !CodeManager.CheckCodeFormat(techSetCode.Password))
		{
			teckCodeTips.Visibility = Visibility.Visible;
			return false;
		}
		isChecked = operatorEnable.IsChecked;
		flag = true;
		if (isChecked == true == flag && isChecked.HasValue && !CodeManager.CheckCodeFormat(operatorSetCode.Password))
		{
			operatorCodeTips.Visibility = Visibility.Visible;
			return false;
		}
		return true;
	}

	private void SaveInfo()
	{
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
	}

	private void ClearXamlData()
	{
		techEnable.IsChecked = false;
		techCode.IsEnabled = false;
		techCode.Text = "";
		techAccessSet.IsEnabled = false;
		techSetCode.Password = "";
		teckCodeTips.Visibility = Visibility.Collapsed;
		operatorEnable.IsChecked = false;
		operatorCode.IsEnabled = false;
		operatorCode.Text = "";
		operatorSetCode.Password = "";
		operatorCodeTips.Visibility = Visibility.Collapsed;
	}

	private void techEnable_Checked(object sender, RoutedEventArgs e)
	{
		techCode.IsEnabled = true;
		techAccessSet.IsEnabled = true;
	}

	private void techEnable_Unchecked(object sender, RoutedEventArgs e)
	{
		techCode.IsEnabled = false;
		techCode.Text = "";
		techAccessSet.IsEnabled = false;
		techSetCode.Password = "";
		teckCodeTips.Visibility = Visibility.Collapsed;
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
		operatorCodeTips.Visibility = Visibility.Collapsed;
	}

	private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = false;
		e.Handled = true;
	}

	private bool CheckPassAdminAccess()
	{
		bool bEnable = false;
		string adminCode = "";
		CodeManager.GetInstance().GetAdminCode(out bEnable, out adminCode);
		if (bEnable)
		{
			AccesssCodeCheck accesssCodeCheck = new AccesssCodeCheck();
			accesssCodeCheck.AccessAdministatorCode = adminCode;
			if (false == accesssCodeCheck.ShowDialog())
			{
				return false;
			}
			return true;
		}
		return true;
	}

	private void ImportPermission_Click(object sender, RoutedEventArgs e)
	{
		if (!CheckPassAdminAccess())
		{
			return;
		}
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "Config Files (*.txt)|*.txt"
		};
		try
		{
			btnImportPermission.IsEnabled = false;
			string empty = string.Empty;
			string access = string.Empty;
			openFileDialog.ValidateNames = true;
			openFileDialog.CheckFileExists = true;
			openFileDialog.CheckPathExists = true;
			if (openFileDialog.ShowDialog() != true)
			{
				return;
			}
			empty = openFileDialog.FileName;
			using (StreamReader streamReader = new StreamReader(empty))
			{
				access = streamReader.ReadToEnd().ToString();
			}
			if (ImportHandleAccessInfo(access))
			{
				InitInfo();
				if (AccessUser.Admin == myCodeManager.GetLoginUser())
				{
					accessparam.Visibility = Visibility.Visible;
				}
				else
				{
					accessparam.Visibility = Visibility.Collapsed;
				}
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LoadSuccess"), MessageButtons.Normal);
			}
			else
			{
				LogHelper.Error("处理用户权限导入信息失败");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LoadFail"), MessageButtons.Normal);
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("用户权限信息导入失败意外错误" + ex.Message);
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LoadFail"), MessageButtons.Normal);
		}
		finally
		{
			btnImportPermission.IsEnabled = true;
		}
	}

	private void ExportPermission_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			btnExportPermission.IsEnabled = false;
			using FileSaveDialog fileSaveDialog = new FileSaveDialog
			{
				FileTypeFilters = new FileTypeFilterCollection
				{
					new FileTypeFilter("Config File", ".txt")
				},
				RestoreDirectory = true,
				DefaultFileExtension = ".txt"
			};
			if (fileSaveDialog.Show(BaseWindow.LastWindow) != FileDialogResult.Ok)
			{
				return;
			}
			string access = ConfigurationHelper.GetConfig(CodeManager.GetInstance().accessInfoNodeName);
			if (string.IsNullOrEmpty(access))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_AccessNotExist"), MessageButtons.Normal);
				return;
			}
			if (!ExportHandleAccessInfo(ref access))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_AccessHandleFailed"), MessageButtons.Normal);
				return;
			}
			using (FileStream fileStream = new FileStream(fileSaveDialog.ShellFile.Path, FileMode.Create, FileAccess.Write))
			{
				byte[] bytes = Encoding.Default.GetBytes(access);
				fileStream.Write(bytes, 0, bytes.Length);
			}
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ExportSuccess"), MessageButtons.Normal);
		}
		catch (Exception ex)
		{
			LogHelper.Error("用户权限信息导出失败意外错误" + ex.Message);
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ExportFailed"), MessageButtons.Normal);
		}
		finally
		{
			btnExportPermission.IsEnabled = true;
		}
	}

	private bool ImportHandleAccessInfo(string access)
	{
		try
		{
			if (string.IsNullOrEmpty(access))
			{
				return false;
			}
			string config = ConfigurationHelper.GetConfig("AccessCheckDisk");
			byte[] array = Convert.FromBase64String(access);
			if (array == null)
			{
				return false;
			}
			object obj = CodeManager.GetInstance().BytesToStruct(array, typeof(AccessUserInfo));
			if (obj == null)
			{
				return false;
			}
			AccessUserInfo accessUserInfo = (AccessUserInfo)obj;
			if (config == "1" && accessUserInfo.adminEnable)
			{
				accessUserInfo.IDInfo = CodeManager.GetDiskID();
			}
			CodeManager.GetInstance()._accessCurUserInfo = accessUserInfo;
			byte[] inArray = CodeManager.GetInstance().StructTOBytes(accessUserInfo);
			string defaultValue = Convert.ToBase64String(inArray);
			ConfigurationHelper.SaveConfig(CodeManager.GetInstance().accessInfoNodeName, defaultValue);
			AccessManagerHelper.SetLoginUser(CodeManager.GetInstance()._accessCurUserInfo.curLoginUser);
			return true;
		}
		catch
		{
			access = "";
			return false;
		}
	}

	private bool ExportHandleAccessInfo(ref string access)
	{
		try
		{
			byte[] array = Convert.FromBase64String(access);
			if (array == null)
			{
				return false;
			}
			object obj = CodeManager.GetInstance().BytesToStruct(array, typeof(AccessUserInfo));
			if (obj == null)
			{
				return false;
			}
			AccessUserInfo accessUserInfo = (AccessUserInfo)obj;
			accessUserInfo.IDInfo = "";
			byte[] inArray = CodeManager.GetInstance().StructTOBytes(accessUserInfo);
			string text = Convert.ToBase64String(inArray);
			access = text;
			return true;
		}
		catch
		{
			access = "";
			return false;
		}
	}
}
