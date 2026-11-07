using System;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class Button_ResetAlgoParams : NameValueItem
{
	private DelegateCommand _clickCommand;

	public ICommand ClickCommand
	{
		get
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			//IL_0024: Expected Obj, but got Unknown
			DelegateCommand val = _clickCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)ClickCommandCmdExecute);
				DelegateCommand val3 = val2;
				_clickCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	private void ClickCommandCmdExecute()
	{
		if (!(GetTopBaseNode() is AlgorithmTabRoot { Owner: VmModule owner } algorithmTabRoot))
		{
			return;
		}
		if (IsUnSupportedAlgoResetModule(owner.StrModuleName))
		{
			string strMsg = AppLocalizationService.GetLang("Lang_UnSupportAlgoParamReset") + "!";
			HikMessageBox.Show(strMsg, MessageButtons.Normal, MessageIcons.Error);
		}
		else if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Sure"), AppLocalizationService.GetLang("Lang_ResetAlgoParamTips") + "?", MessageButtons.YesOrNo, MessageIcons.Question) == DialogResult.Yes)
		{
			try
			{
				algorithmTabRoot.ResetRunParamBind();
				owner.ResetParam();
			}
			catch (Exception ex)
			{
				string lang = AppLocalizationService.GetLang(ex.Message);
				HikMessageBox.Show(lang, MessageIcons.Error);
				LogHelper.objLog.Error(lang);
				return;
			}
			NameValueItem._IsResetAlgoParamsing = true;
			algorithmTabRoot.GetItemsValueUnit();
			NameValueItem._IsResetAlgoParamsing = false;
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ResetAlgoParamSuccess"), MessageButtons.Normal, MessageIcons.Ok);
		}
	}

	internal bool IsUnSupportedAlgoResetModule(string strModuleName)
	{
		if (strModuleName == "IMVSEdgeInspGroupModu" || strModuleName == "IMVSEdgePairInspGroupModu" || strModuleName == "IMVSImageCombineProcessModu")
		{
			return true;
		}
		return false;
	}
}
