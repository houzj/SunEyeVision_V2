using System;
using Apps.Localization;
using Apps.Log;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class SetMaxModuleNumOperation : IOperation
{
	public string TargetName = "";

	public Setter Executor { get; set; }

	public bool Working
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public SetMaxModuleNumOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		try
		{
			ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetMaxModuleNum(Convert.ToUInt32(parentNode.Value));
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				if (parentNode is IConfirm confirm)
				{
					confirm.MessageText = AppLocalizationService.GetLang("SetMaxModuleNumErrorInfo") + ex2.errorMessage;
				}
				LogHelper.Error("SetMaxModuleNum is error:" + ex2.errorMessage);
			}
			LogHelper.Error(ex);
		}
	}

	public void ReOperate()
	{
	}
}
