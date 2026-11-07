using System;
using Apps.Localization;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class OpenFileForDataShellDialog : OpenFileDialog
{
	public override string FilterName => AppLocalizationService.GetLang("Lang_ModelScriptFilter");

	protected override void ValueChangedCmdExecute()
	{
		if (CurValueA == null)
		{
			return;
		}
		try
		{
			if (SetParamUnit() != 0)
			{
				HikMessageBox.Show("Lang_FileNameSetFail", "Lang_ModuErrtoDataSet", MessageIcons.Error, isTranslate: true);
				return;
			}
			bool flag = true;
			AlgorithmTabRoot algorithmTabRoot = GetTopBaseNode() as AlgorithmTabRoot;
			object obj = null;
			if (algorithmTabRoot != null)
			{
				foreach (Tab tab in algorithmTabRoot.Tabs)
				{
					IBottomCommand bottomCommand = tab.FindBaseNodeByName("txtOutputParams") as IBottomCommand;
					obj = tab.FindBaseNodeByName("shellOutputParams");
					if (bottomCommand != null)
					{
						flag = (bool)bottomCommand.ExecuteFunction(true);
					}
				}
			}
			if (flag)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SetSuc"), MessageIcons.Ok);
			}
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
