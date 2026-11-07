using System;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class SetMVDWarmupGpuEnableOperation : IOperation
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

	public SetMVDWarmupGpuEnableOperation(string TName)
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
			int num = -536870657;
			uint nModuleID = 0u;
			string strName = "MVDWarmupGpuEnable";
			num = ServerSDKManager.serverSDKManager.mSolutionManager.SetGlobalConfig(nModuleID, strName, parentNode.Value.ToString());
			if (num != 0)
			{
				VmException ex = new VmException(num);
				throw ex;
			}
		}
		catch (Exception ex2)
		{
			if (ex2 is VmException ex3)
			{
				if (parentNode is IConfirm confirm)
				{
					confirm.MessageText = AppLocalizationService.GetLang("SetMVDWarmupGpuEnableErrorInfo") + ErrorCodeInfo.GetErrorInfo(ex3.errorCode);
				}
				LogHelper.Error("MVDWarmupGpuEnable is error:" + ex3.errorMessage);
			}
			LogHelper.Error(ex2);
		}
	}

	public void ReOperate()
	{
	}
}
