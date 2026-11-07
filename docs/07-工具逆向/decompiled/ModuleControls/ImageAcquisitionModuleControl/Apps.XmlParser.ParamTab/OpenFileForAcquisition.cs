using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Threading;
using Apps.Localization;
using Apps.Log;
using Microsoft.WindowsAPICodePack.Dialogs;
using VM.Controls;
using VM.Utility;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class OpenFileForAcquisition : NameValueItem
{
	public int MaxFileDirLength = 200;

	private const int PATH_MAX = 255;

	public string CurValue;

	private DelegateCommand _ClickCommand;

	private long curTime = DateTime.Now.Ticks;

	public string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FilePathEptErr"), MessageButtons.Normal, MessageIcons.Error);
			}
			else
			{
				if (!(CurValue != value))
				{
					return;
				}
				if (!IsCorrectDir(value))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FilePathFmtErr"), MessageButtons.Normal, MessageIcons.Error);
					return;
				}
				if (value.Length > MaxFileDirLength)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FilePathLenErr"), MessageButtons.Normal, MessageIcons.Error);
					return;
				}
				if (value.Length >= 3 && !Directory.Exists(value.Substring(0, 3)))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FilePathErr"), MessageButtons.Normal, MessageIcons.Error);
					return;
				}
				if (!Directory.Exists(value))
				{
					try
					{
						Directory.CreateDirectory(value);
					}
					catch
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FilePathCharErr"), MessageButtons.Normal, MessageIcons.Error);
						return;
					}
				}
				if (!IsHaveRWAccess(value, ReadOrWrite: false))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileRdOnly"), MessageButtons.Normal, MessageIcons.Error);
					return;
				}
				CurValue = value;
				OnPropertyChanged("CurValueA");
				ValueChangedCmdExecute();
			}
		}
	}

	public override string DisplayType => "OpenFileForAcquisition";

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
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand(ClickCommandCmdExecute);
			}
			return _ClickCommand;
		}
	}

	private void ClickCommandCmdExecute()
	{
		DoOpenFile();
	}

	private void DoOpenFile()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected Obj, but got Unknown
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Invalid comparison between Unknown and I4
		CommonOpenFileDialog val = new CommonOpenFileDialog();
		val.IsFolderPicker = true;
		val.Multiselect = false;
		CommonFileDialogResult val2 = ((CommonFileDialog)val).ShowDialog();
		try
		{
			if ((int)val2 != 2)
			{
				CurValueA = ((CommonFileDialog)val).FileName.Trim();
				AddImageByDir(CurValueA);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageButtons.Normal, MessageIcons.Error);
		}
		finally
		{
			((CommonFileDialog)val).Dispose();
		}
	}

	private void ValueChangedCmdExecute()
	{
		if (!IsUnConnect())
		{
			if (SetParamUnit() != 0)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DrOpenFail"), MessageButtons.Normal, MessageIcons.Error);
				return;
			}
			((IUserStringData)GetVmModule().Params)["ClearImage"] = "";
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DrOpenSuc"), MessageButtons.Normal, MessageIcons.Ok);
		}
	}

	private bool IsHaveRWAccess(string dir, bool ReadOrWrite)
	{
		if (!ReadOrWrite)
		{
			try
			{
				string path = dir + "\\test.txt";
				File.Create(path).Close();
				File.Delete(path);
				return true;
			}
			catch (UnauthorizedAccessException)
			{
				return false;
			}
		}
		try
		{
			Directory.GetFiles(dir);
			return true;
		}
		catch (UnauthorizedAccessException)
		{
			return false;
		}
	}

	private bool IsCorrectDir(string dir)
	{
		return new Regex("^[A-Za-z][:][\\\\][^\f\n\r\t\v]*$").IsMatch(dir);
	}

	private void AddImageByDir(string dir, int folderLayer = 1)
	{
		try
		{
			if (!Directory.Exists(dir))
			{
				return;
			}
			string[] array = (from x in Directory.GetFileSystemEntries(dir)
				orderby x.Length, x
				select x).ToArray();
			foreach (string text in array)
			{
				if (File.Exists(text))
				{
					if (!(Path.GetExtension(text).ToUpper() != ".JPG") || !(Path.GetExtension(text).ToUpper() != ".JPEG") || !(Path.GetExtension(text).ToUpper() != ".BMP") || !(Path.GetExtension(text).ToUpper() != ".PNG") || !(Path.GetExtension(text).ToUpper() != ".TIF") || !(Path.GetExtension(text).ToUpper() != ".TIFF"))
					{
						if (!AddImage(text))
						{
							break;
						}
						OnTimeRefreshView();
					}
				}
				else if (folderLayer <= 1)
				{
					AddImageByDir(text, folderLayer + 1);
				}
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImageAdd"), AppLocalizationService.GetLang("Lang_ReadImageFail"), MessageIcons.Error);
			LogHelper.Error("Message:" + ex.Message + "StackTrace:" + ex.StackTrace);
		}
	}

	private void OnTimeRefreshView()
	{
		if (DateTime.Now.Ticks - curTime > 1000000)
		{
			DoEvents();
			curTime = DateTime.Now.Ticks;
		}
	}

	public void DoEvents()
	{
		DispatcherFrame dispatcherFrame = new DispatcherFrame();
		Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (DispatcherOperationCallback)((object f) =>
		{
			((DispatcherFrame)f).Continue = false;
			return (object)null;
		}), dispatcherFrame);
		Dispatcher.PushFrame(dispatcherFrame);
	}

	private bool AddImage(string fileName)
	{
		if (!AddImageToServer(fileName))
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ImageAdd"), AppLocalizationService.GetLang("Lang_ImagePathError"), MessageIcons.Error);
			return false;
		}
		return true;
	}

	private bool AddImageToServer(string fileName)
	{
		if (GetVmModule() != null)
		{
			try
			{
				((IUserStringData)GetVmModule().Params)["AddImage"] = fileName;
			}
			catch (Exception)
			{
				return false;
			}
		}
		return true;
	}
}
