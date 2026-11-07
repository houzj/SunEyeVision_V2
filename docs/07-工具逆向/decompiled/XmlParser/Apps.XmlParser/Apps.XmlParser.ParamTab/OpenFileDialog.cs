using System;
using System.IO;
using System.Windows.Input;
using Apps.Localization;
using DirectoryBrowser;
using Microsoft.Win32;
using Mvvm.Commands;
using VM.Controls;
using VM.Utility;
using VM.Utility.SingleInstance;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class OpenFileDialog : NameValueItem, IGroupRunParam
{
	public string FileInitDirectory = null;

	public string SaveType = null;

	public string CurValue;

	public HikFileOption FileOption;

	private DelegateCommand _ClickCommand = null;

	private string fullpath = string.Empty;

	private const string STR_REMOTE_TEMP_FILE = "/ftp/ModelFiles/";

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

	public virtual string FilterName => AppLocalizationService.GetLang(FileOption.FilterName);

	protected bool IsUploadServer => RemoteOperate.IsRemoteMode;

	public string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				CurValue = value;
				ValueChangedCmdExecute();
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public override bool NeedInit => false;

	public override string Value
	{
		get
		{
			return CurValueA;
		}
		set
		{
			CurValue = value;
			OnPropertyChanged("CurValueA");
		}
	}

	public ICommand ClickCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand((Action)ClickCommandCmdExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	public IVmModule OwnerModule { get; set; }

	string IGroupRunParam.DisplayForGroupType => null;

	protected virtual void ClickCommandCmdExecute()
	{
		DoOpenFile();
	}

	private void DoOpenFile()
	{
		IVmModule vmModule = OwnerModule ?? ((GetTopBaseNode() as AlgorithmTabRoot).Owner as IVmModule);
		if (!IsUploadServer)
		{
			Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
			openFileDialog.CheckFileExists = true;
			openFileDialog.CheckPathExists = true;
			openFileDialog.Filter = FilterName;
			openFileDialog.Title = AppLocalizationService.GetLang("Open");
			FileInitDirectory = ConfigurationHelper.INIGetDefaultOpenPath(vmModule?.ModuleDir);
			try
			{
				if (!string.IsNullOrEmpty(CurValueA) && File.Exists(CurValueA))
				{
					FileInfo fileInfo = new FileInfo(CurValueA);
					FileInitDirectory = fileInfo.DirectoryName;
				}
				if (!string.IsNullOrEmpty(FileInitDirectory) && Directory.Exists(FileInitDirectory))
				{
					openFileDialog.InitialDirectory = FileInitDirectory;
				}
				if (openFileDialog.ShowDialog() == true)
				{
					string fileName = openFileDialog.FileName;
					SetCurValue(fileName);
					ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(fileName), vmModule?.ModuleDir);
				}
				return;
			}
			catch (Exception ex)
			{
				HikMessageBox.Show(ex.Message, MessageButtons.Normal, MessageIcons.Error);
				return;
			}
		}
		try
		{
			FileInitDirectory = ConfigurationHelper.INIGetDefaultOpenRemotePath(vmModule?.ModuleDir);
			DirectoryBrowser.OpenFileDialog openFileDialog2 = new DirectoryBrowser.OpenFileDialog
			{
				CheckFileExists = true,
				Filter = FilterName,
				InitialDirectory = FileInitDirectory,
				Title = AppLocalizationService.GetLang("Open")
			};
			if (openFileDialog2.ShowDialog() == true)
			{
				string curValue = openFileDialog2.FileNames[0];
				SetCurValue(curValue);
				string directoryName = openFileDialog2.GetDirectoryName();
				ConfigurationHelper.INISetDefaultOpenRemotePath(directoryName, vmModule?.ModuleDir);
			}
		}
		catch (Exception ex2)
		{
			HikMessageBox.Show(ex2.Message, MessageButtons.Normal, MessageIcons.Error);
		}
	}

	protected virtual void SetCurValue(string value)
	{
		CurValueA = null;
		CurValueA = value;
	}

	protected virtual void ValueChangedCmdExecute()
	{
		if (IsUnConnect() || CurValueA == null)
		{
			return;
		}
		try
		{
			if (SetParamUnit() == 0)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SetSuc"), MessageIcons.Ok);
				PreExecuteRun();
			}
		}
		catch (Exception ex)
		{
			string[] strArrMsg = new string[3]
			{
				AppLocalizationService.GetLang("Lang_SysErr"),
				":",
				ex.Message
			};
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileOpenFail"), strArrMsg, MessageIcons.Error);
		}
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

	IDecorate IGroupRunParam.SupportDisplayForGroupData()
	{
		return null;
	}
}
