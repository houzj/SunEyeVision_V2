using System;
using System.Windows.Input;
using Apps.Localization;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class Command_FixtureModule : Command
{
	private new const int IMVS_E_MODU_CALLORDER = -1610612734;

	private DelegateCommand _ClickCommand;

	public override string Value
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
		}
	}

	public override ICommand ClickCommand
	{
		get
		{
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand((Action)ClickCommandCmdExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	private void ClickCommandCmdExecute()
	{
		if ("1" == CurValueA)
		{
			DialogResult dialogResult = HikMessageBox.Show("Lang_InfoTips", "Lang_FdPExitedIfNew", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
			if (DialogResult.SDKModel != dialogResult && (DialogResult.No == dialogResult || dialogResult == DialogResult.None))
			{
				return;
			}
		}
		int num = CreateDatumPoint();
		if (1 == num)
		{
			CurValue = "1";
		}
		else
		{
			CurValue = "0";
		}
	}

	private int CreateDatumPoint()
	{
		if (!CheckInputParameters(out var msg))
		{
			HikMessageBox.Show("错误提示", AppLocalizationService.GetLang("基准点创建失败") + " , " + msg + " !", MessageButtons.Normal, MessageIcons.Error, isTranslate: true);
			return 0;
		}
		int num = SetParamUnit();
		if (num != 0)
		{
			if (-1610612734 == num)
			{
				HikMessageBox.Show("Lang_Err", "Lang_FdPCreateErr", MessageButtons.Normal, MessageIcons.Error, isTranslate: true);
				return 0;
			}
			return 0;
		}
		HikMessageBox.Show("Lang_InfoTips", "Lang_FdPCreateSuc", MessageButtons.Normal, MessageIcons.Ok, isTranslate: true);
		return 1;
	}

	public override int GetParamUnit()
	{
		return GetParamUnitByZmq();
	}

	private bool CheckInputParameters(out string msg)
	{
		msg = string.Empty;
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			try
			{
				if (!(algorithmTabRoot.Owner is VmModule { IsReady: var isReady }))
				{
					msg = AppLocalizationService.GetLang("Fixture") + AppLocalizationService.GetLang("Lang_Err") + "！";
					return false;
				}
				if (!isReady.Value)
				{
					msg = AppLocalizationService.GetLang("Tab_Params") + AppLocalizationService.GetLang("未配置");
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				msg = ex.Message;
				return false;
			}
		}
		return false;
	}
}
