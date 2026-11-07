using System;
using System.IO;
using System.Linq;
using Apps.Localization;
using Microsoft.Win32;
using VM.Controls;
using VM.Utility;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class OpenFileForCameraDialog : OpenFileDialog
{
	public override string DisplayType => "OpenFileDialog";

	protected override void ClickCommandCmdExecute()
	{
		DoOpenFile();
	}

	private void DoOpenFile()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
		openFileDialog.CheckFileExists = false;
		openFileDialog.CheckPathExists = true;
		openFileDialog.Filter = FilterName;
		IVmModule vmModule = (GetTopBaseNode() as AlgorithmTabRoot).Owner as IVmModule;
		FileInitDirectory = ConfigurationHelper.INIGetDefaultOpenPath(vmModule?.ModuleDir);
		if (FileInitDirectory != null)
		{
			openFileDialog.InitialDirectory = FileInitDirectory;
		}
		try
		{
			if (openFileDialog.ShowDialog() == true)
			{
				CurValueA = null;
				string path = (CurValueA = openFileDialog.FileName);
				ConfigurationHelper.INISetDefaultOpenPath(Path.GetDirectoryName(path), vmModule?.ModuleDir);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageButtons.Normal, MessageIcons.Error);
		}
	}

	protected override void ValueChangedCmdExecute()
	{
		if (CurValueA == null)
		{
			return;
		}
		string curValueA = CurValueA;
		string[] array = FilterName.Split('|');
		if (array.Count() <= 1)
		{
			return;
		}
		string text = array[1];
		string[] array2 = text.Split(';');
		bool flag = false;
		string[] array3 = array2;
		foreach (string text2 in array3)
		{
			if (curValueA.EndsWith(text2.Substring(1, text2.Length - 1)))
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
		if (!File.Exists(curValueA))
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileOpenFail"), MessageIcons.Error);
			return;
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
}
