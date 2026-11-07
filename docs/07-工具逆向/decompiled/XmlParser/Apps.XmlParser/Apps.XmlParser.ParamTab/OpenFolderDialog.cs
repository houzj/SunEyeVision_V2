using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Input;
using Apps.Localization;
using DirectoryBrowser;
using Mvvm.Commands;
using VM.Controls;
using VM.Utility.SingleInstance;

namespace Apps.XmlParser.ParamTab;

public class OpenFolderDialog : NameValueItem
{
	public int MaxFileDirLength = 200;

	public string CurValue;

	private DelegateCommand _ClickCommand = null;

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
				if (RemoteOperate.IsRemoteMode)
				{
					if (value.Length > MaxFileDirLength)
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FilePathLenErr"), MessageButtons.Normal, MessageIcons.Error);
						return;
					}
				}
				else
				{
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
					if (value.Length >= 3)
					{
						string path = value.Substring(0, 3);
						if (!Directory.Exists(path))
						{
							HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FilePathErr"), MessageButtons.Normal, MessageIcons.Error);
							return;
						}
					}
					if (!Directory.Exists(value))
					{
						try
						{
							DirectoryInfo directoryInfo = Directory.CreateDirectory(value);
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
				}
				CurValue = value;
				OnPropertyChanged("CurValueA");
				ValueChangedCmdExecute();
			}
		}
	}

	public override string DisplayType => "OpenFileDialog";

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
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand((Action)ClickCommandCmdExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	private void ClickCommandCmdExecute()
	{
		DoOpenFile();
	}

	private void DoOpenFile()
	{
		if (RemoteOperate.IsRemoteMode)
		{
			DirectoryBrowser.FolderBrowserDialog folderBrowserDialog = new DirectoryBrowser.FolderBrowserDialog();
			try
			{
				if (folderBrowserDialog.ShowDialog() == true)
				{
					CurValueA = folderBrowserDialog.FilePath.Trim();
				}
				return;
			}
			catch (Exception ex)
			{
				HikMessageBox.Show(ex.Message, MessageButtons.Normal, MessageIcons.Error);
				return;
			}
		}
		System.Windows.Forms.FolderBrowserDialog folderBrowserDialog2 = new System.Windows.Forms.FolderBrowserDialog();
		try
		{
			if (folderBrowserDialog2.ShowDialog() != System.Windows.Forms.DialogResult.Cancel)
			{
				CurValueA = folderBrowserDialog2.SelectedPath.Trim();
			}
		}
		catch (Exception ex2)
		{
			HikMessageBox.Show(ex2.Message, MessageButtons.Normal, MessageIcons.Error);
		}
		finally
		{
			folderBrowserDialog2.Dispose();
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
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_DrOpenSuc"), MessageButtons.Normal, MessageIcons.Ok);
			PreExecuteRun();
		}
	}

	private bool IsHaveRWAccess(string dir, bool ReadOrWrite)
	{
		if (!ReadOrWrite)
		{
			try
			{
				string path = dir + "\\test.txt";
				FileStream fileStream = File.Create(path);
				fileStream.Close();
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
		string pattern = "^[A-Za-z][:][\\\\][^\f\n\r\t\v]*$";
		Regex regex = new Regex(pattern);
		return regex.IsMatch(dir);
	}
}
