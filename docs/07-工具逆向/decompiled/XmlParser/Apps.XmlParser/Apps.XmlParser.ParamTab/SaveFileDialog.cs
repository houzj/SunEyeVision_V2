using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using DirectoryBrowser;
using Microsoft.Win32;
using VM.Controls;
using VM.PlatformSDKCS;
using VM.Utility;
using VM.Utility.SingleInstance;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class SaveFileDialog : NameValueItem
{
	public HikFileOption FileOption;

	public string FileInitDirectory = null;

	public string SaveType = null;

	private DelegateCommand _ClickCommand = null;

	private ItemState _MyItemState = ItemState.Normal;

	public string SaveTypeA
	{
		get
		{
			return SaveType;
		}
		set
		{
			SaveType = value;
		}
	}

	public override bool NeedInit => false;

	public override string DisplayType => "Command";

	public ICommand ClickCommand
	{
		get
		{
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand(ClickCommandCmdExecute);
			}
			return _ClickCommand;
		}
	}

	public override ItemState MyItemState
	{
		get
		{
			return _MyItemState = ItemState.Normal;
		}
		set
		{
			_MyItemState = ItemState.Normal;
		}
	}

	private void ClickCommandCmdExecute()
	{
		DoSaveFile();
	}

	private bool isRemoteMode()
	{
		return RemoteOperate.IsRemoteMode;
	}

	protected void SaveFile(string filePath)
	{
		((IUserStringData)GetModuleTagInfo())[Name] = filePath;
		if (!(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
		{
			return;
		}
		foreach (Tab tab in algorithmTabRoot.Tabs)
		{
			object obj = tab.FindBaseNodeByName("CalibPathName");
			if (obj != null && obj is NameValueItem)
			{
				(obj as NameValueItem).GetParamUnit();
				break;
			}
		}
	}

	protected void SaveRemoteFile()
	{
		IVmModule vmModule = (GetTopBaseNode() as AlgorithmTabRoot).Owner as IVmModule;
		FileInitDirectory = ConfigurationHelper.INIGetDefaultOpenRemotePath(vmModule?.ModuleDir);
		DirectoryBrowser.SaveFileDialog saveFileDialog = new DirectoryBrowser.SaveFileDialog
		{
			Filter = FileOption.FilterName,
			InitialDirectory = FileInitDirectory
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		string[] array = saveFileDialog.Filter.Split('|');
		if (array.Count() != 2)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), MessageIcons.Error);
			return;
		}
		if (!saveFileDialog.FilePath.EndsWith(array[0]))
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), AppLocalizationService.GetLang("Lang_FilePathTooLong"), MessageIcons.Error);
			return;
		}
		try
		{
			SaveFile(saveFileDialog.FilePath);
			string directoryName = saveFileDialog.GetDirectoryName();
			ConfigurationHelper.INISetDefaultOpenRemotePath(directoryName, vmModule?.ModuleDir);
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SaveSuccess"), MessageIcons.Ok);
		}
		catch (VmException ex)
		{
			string[] strArrMsg = new string[3]
			{
				AppLocalizationService.GetLang("Lang_SysErr"),
				":",
				ErrorCodeInfo.GetErrorInfo(ex.errorCode)
			};
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), strArrMsg, MessageIcons.Error);
		}
		catch (Exception ex2)
		{
			string[] strArrMsg2 = new string[3]
			{
				AppLocalizationService.GetLang("Lang_SysErr"),
				":",
				ex2.Message
			};
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), strArrMsg2, MessageIcons.Error);
		}
	}

	protected void SaveLocalFile()
	{
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog();
		saveFileDialog.Filter = FileOption.FilterName;
		saveFileDialog.FilterIndex = 1;
		saveFileDialog.RestoreDirectory = true;
		IVmModule vmModule = (GetTopBaseNode() as AlgorithmTabRoot).Owner as IVmModule;
		FileInitDirectory = ConfigurationHelper.INIGetDefaultOpenPath(vmModule?.ModuleDir);
		if (FileInitDirectory != null)
		{
			saveFileDialog.InitialDirectory = FileInitDirectory;
		}
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		string[] array = saveFileDialog.Filter.Split('|');
		if (array.Count() != 2)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), MessageIcons.Error);
			return;
		}
		if (!saveFileDialog.FileName.EndsWith(array[0]))
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), AppLocalizationService.GetLang("Lang_FilePathTooLong"), MessageIcons.Error);
			return;
		}
		try
		{
			SaveFile(saveFileDialog.FileName);
			ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(saveFileDialog.FileName), vmModule?.ModuleDir);
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SaveSuccess"), MessageIcons.Ok);
		}
		catch (VmException ex)
		{
			string[] strArrMsg = new string[3]
			{
				AppLocalizationService.GetLang("Lang_SysErr"),
				":",
				ErrorCodeInfo.GetErrorInfo(ex.errorCode)
			};
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), strArrMsg, MessageIcons.Error);
		}
		catch (Exception ex2)
		{
			string[] strArrMsg2 = new string[3]
			{
				AppLocalizationService.GetLang("Lang_SysErr"),
				":",
				ex2.Message
			};
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileSaveFail"), strArrMsg2, MessageIcons.Error);
		}
	}

	public void DoSaveFile()
	{
		if (FileOption != null)
		{
			if (isRemoteMode())
			{
				SaveRemoteFile();
			}
			else
			{
				SaveLocalFile();
			}
		}
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}

	public void PathInit()
	{
		if (!(SaveTypeA == "CalibPath"))
		{
		}
	}

	public void PathSave()
	{
		if (!(SaveTypeA == "CalibPath"))
		{
		}
	}
}
