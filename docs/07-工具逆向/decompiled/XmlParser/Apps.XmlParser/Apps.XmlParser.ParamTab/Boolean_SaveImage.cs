using System;
using System.Windows.Input;
using Apps.ErrorCode;
using Apps.Localization;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class Boolean_SaveImage : Boolean
{
	private DelegateCommand _ClickCommand = null;

	public ICommand ClickCommand
	{
		get
		{
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			return (_ClickCommand != null) ? ((ICommand)_ClickCommand) : ((ICommand)new DelegateCommand((Action)ClickCommandExecute));
		}
	}

	private void ClickCommandExecute()
	{
		VmModule vmModule = GetVmModule();
		if (vmModule != null)
		{
			try
			{
				string strValue = string.Empty;
				ServerSDKManager.serverSDKManager.mModuleManager.GetParamValue(vmModule.ID, "TestFtp", ref strValue, bThrowException: true);
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ConnectSuccess"), MessageIcons.Ok);
				return;
			}
			catch (VmException ex)
			{
				string strMsg = AppLocalizationService.GetLang("Lang_ConnectFailed") + Environment.NewLine + "(" + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(ex.errorCode) + ")";
				HikMessageBox.Show(strMsg, MessageIcons.Error);
				return;
			}
		}
		HikMessageBox.Show(AppLocalizationService.GetLang("模块不存在"), MessageIcons.Error);
	}
}
