using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Apps.Localization;
using Apps.Log;
using DirectoryBrowser;
using HikPcUI.Commom;
using Microsoft.Win32;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VM.Utility.SingleInstance;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class OpenFileForCalibDialog : OpenFileDialog, ISubInfo
{
	private bool isInit = false;

	private bool IsSub = false;

	private ButtonSelecter _SubData;

	public bool IsSubA
	{
		get
		{
			InitIsSub();
			return IsSub;
		}
		set
		{
			if (IsSub == value)
			{
				return;
			}
			IsSub = value;
			if (IsSub)
			{
				NameValueItem.logManager?.LogCreate("信息", AppLocalizationService.GetLang("Lang_SwitchSubscribe") ?? "");
			}
			else
			{
				NameValueItem.logManager?.LogCreate("信息", AppLocalizationService.GetLang("Lang_SwitchEdit") ?? "");
			}
			try
			{
				VmModule vmModule = GetVmModule();
				if (vmModule != null)
				{
					if (IsSub)
					{
						(SubData.InitersA[0] as Setter).Operate(new object[1] { SubData });
					}
					else if (_SubData != null && !string.IsNullOrEmpty(_SubData.CurValueA))
					{
						_SubData.CurValueA = null;
						GetParamUnit();
					}
					vmModule.UserData["@" + ComName] = StringUtil.UTF8GetBytesPadZero(IsSub.ToString());
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex.Message);
			}
			OnPropertyChanged("IsSubA");
		}
	}

	public string ComType { get; set; } = "string";

	public ButtonSelecter SubData
	{
		get
		{
			if (_SubData == null)
			{
				_SubData = new ButtonSelecter();
				_SubData.ReadOnlyA = true;
				_SubData.DropOpenSetter = new Setter
				{
					TargetName = "EnumEntrys",
					OperationName = "GetFrontFilterTreeOperation",
					OperationParams = ComType
				};
				_SubData.DropOpenSetter.SetParentNode(_SubData);
				_SubData.Triggers = new ObservableCollection<object>();
				Trigger trigger = new Trigger
				{
					Property = "CurValue"
				};
				Setter setter = new Setter
				{
					OperationName = "SetModuleParamBindOperation",
					OperationParams = ComName
				};
				setter.CreatOperateInstance();
				trigger.Setters = new ObservableCollection<object>();
				trigger.Setters.Add(setter);
				setter.SetParentNode(trigger);
				_SubData.Triggers.Add(trigger);
				trigger.SetParentNode(_SubData);
				_SubData.Initers = new ObservableCollection<object>();
				Setter setter2 = new Setter
				{
					TargetName = "CurValue",
					OperationName = "GetModuleParamBindOperation",
					OperationParams = ComName
				};
				setter2.CreatOperateInstance();
				_SubData.Initers.Add(setter2);
				_SubData.SetParentNode(this);
				VmModule vmModule = GetVmModule();
				if (vmModule != null)
				{
					string moduleParamBinding = vmModule.ParamSubManager.GetModuleParamBinding(ComName);
					_SubData.SetPropertyValueByNameNotrigger("CurValueA", moduleParamBinding);
				}
			}
			else
			{
				try
				{
					string text = _SubData.CurValueA;
					if (IsSubscribeStr(text))
					{
						int num = text.IndexOf('[', 0);
						if (num > 0)
						{
							text = text.Remove(num);
						}
						VmModule vmModule2 = GetVmModule();
						if (vmModule2 != null)
						{
							VmIO ioByPathFromAround = vmModule2.GetIoByPathFromAround(text);
							if (ioByPathFromAround == null)
							{
								_SubData.CurValueA = null;
							}
						}
					}
				}
				catch (Exception ex)
				{
					LogHelper.Error(ex.Message);
				}
			}
			return _SubData;
		}
	}

	public override bool CustomVisibleA
	{
		get
		{
			return CustomVisible;
		}
		set
		{
			if (CustomVisible == value)
			{
				return;
			}
			CustomVisible = value;
			bool flag = true;
			if (parentNode is Category category)
			{
				flag = category.IsDeleteSubscribeA;
				if (flag)
				{
					flag = IsDeleteSubscribeA;
				}
			}
			if ((!CustomVisible & flag) && _SubData != null && !string.IsNullOrEmpty(_SubData.CurValueA))
			{
				_SubData.CurValueA = null;
				GetParamUnit();
			}
			OnPropertyChanged("CustomVisibleA");
		}
	}

	private void InitIsSub()
	{
		if (isInit)
		{
			return;
		}
		try
		{
			byte[] array = GetVmModule()?.UserData["@" + ComName];
			if (array != null)
			{
				string value = StringUtil.UTF8GetString(array);
				if (bool.TryParse(value, out var result))
				{
					IsSubA = result;
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message);
		}
		isInit = true;
	}

	public void SetInit()
	{
		isInit = false;
		_SubData = null;
	}

	protected override void ClickCommandCmdExecute()
	{
		DoOpenFile();
	}

	private bool isRemoteMode()
	{
		return RemoteOperate.IsRemoteMode;
	}

	private void DoOpenFile()
	{
		if (isRemoteMode())
		{
			IVmModule vmModule = (GetTopBaseNode() as AlgorithmTabRoot).Owner as IVmModule;
			FileInitDirectory = ConfigurationHelper.INIGetDefaultOpenRemotePath(vmModule?.ModuleDir);
			DirectoryBrowser.OpenFileDialog openFileDialog = new DirectoryBrowser.OpenFileDialog
			{
				Filter = FilterName,
				CheckFileExists = false,
				InitialDirectory = FileInitDirectory
			};
			try
			{
				if (openFileDialog.ShowDialog() == true)
				{
					CurValueA = null;
					string curValueA = openFileDialog.FileNames.First();
					CurValueA = curValueA;
					string directoryName = openFileDialog.GetDirectoryName();
					ConfigurationHelper.INISetDefaultOpenRemotePath(directoryName, vmModule?.ModuleDir);
				}
				return;
			}
			catch (Exception ex)
			{
				HikMessageBox.Show(ex.Message, MessageButtons.Normal, MessageIcons.Error);
				return;
			}
		}
		Microsoft.Win32.OpenFileDialog openFileDialog2 = new Microsoft.Win32.OpenFileDialog();
		openFileDialog2.CheckFileExists = false;
		openFileDialog2.CheckPathExists = true;
		openFileDialog2.Filter = FilterName;
		IVmModule vmModule2 = (GetTopBaseNode() as AlgorithmTabRoot).Owner as IVmModule;
		FileInitDirectory = ConfigurationHelper.INIGetDefaultOpenPath(vmModule2?.ModuleDir);
		if (FileInitDirectory != null)
		{
			openFileDialog2.InitialDirectory = FileInitDirectory;
		}
		try
		{
			if (openFileDialog2.ShowDialog() == true)
			{
				CurValueA = null;
				string path = (CurValueA = openFileDialog2.FileName);
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(path), vmModule2?.ModuleDir);
			}
		}
		catch (Exception ex2)
		{
			HikMessageBox.Show(ex2.Message, MessageButtons.Normal, MessageIcons.Error);
		}
	}

	protected override void ValueChangedCmdExecute()
	{
		if (CurValueA == null)
		{
			return;
		}
		string text = CurValueA;
		string[] array = FilterName.Split('|');
		if (array.Count() <= 1)
		{
			return;
		}
		string text2 = array[1];
		string[] array2 = text2.Split(';');
		bool flag = false;
		string[] array3 = array2;
		foreach (string text3 in array3)
		{
			if (text.EndsWith(text3.Substring(1, text3.Length - 1)))
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileOpenFail"), MessageIcons.Error);
			return;
		}
		if (text.StartsWith("/root"))
		{
			int num = text.LastIndexOf('/');
			if (num >= 0 && num < text.Length)
			{
				text = text.Substring(0, num);
			}
			if (ServerSDKAPI.FtpCheckFileName(CVmObjectBase.mhandle, text))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileOpenFail"), MessageIcons.Error);
				LogHelper.Error("文件" + text + "不存在");
				return;
			}
		}
		else
		{
			int num2 = text.LastIndexOf('\\');
			if (num2 >= 0 && num2 < text.Length)
			{
				text = text.Substring(0, num2);
			}
			if (!Directory.Exists(text))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileOpenFail"), MessageIcons.Error);
				return;
			}
		}
		try
		{
			if (SetParamUnit() != 0)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileNameSetFail"), AppLocalizationService.GetLang("Lang_ModuErrtoDataSet"), MessageIcons.Error);
				return;
			}
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SetSuc"), MessageIcons.Ok);
			PreExecuteRun();
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

	public override int GetParamUnit()
	{
		int paramUnit = base.GetParamUnit();
		if (!string.IsNullOrEmpty(CurValueA))
		{
			string text = CurValueA;
			int num = text.LastIndexOf('\\');
			if (num >= 0 && num < text.Length)
			{
				text = text.Substring(0, num);
			}
			if (!Directory.Exists(text))
			{
				CurrentUIHelper.Instance.Dispatcher.BeginInvoke((Action)(() =>
				{
					Thread.Sleep(500);
					HikMessageBox.Show(AppLocalizationService.GetLang("Set Calibration File Path") + AppLocalizationService.GetLang("不存在"), MessageIcons.Warning, MessageButtons.NoButton, 1000u);
				}));
			}
		}
		return paramUnit;
	}
}
