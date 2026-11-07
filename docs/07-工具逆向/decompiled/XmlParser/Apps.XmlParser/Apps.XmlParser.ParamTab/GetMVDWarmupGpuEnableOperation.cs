using System;
using Apps.ErrorCode;
using Apps.Log;
using VM.Controls;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class GetMVDWarmupGpuEnableOperation : IOperation
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

	public GetMVDWarmupGpuEnableOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		string strValue = "false";
		try
		{
			int num = -536870657;
			uint nModuleID = 0u;
			string strName = "MVDWarmupGpuEnable";
			num = ServerSDKManager.serverSDKManager.mSolutionManager.GetGlobalConfig(nModuleID, strName, ref strValue);
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
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex3.errorCode), MessageIcons.Error);
				LogHelper.Error("GetMVDWarmupGpuEnable is error:" + ex3.errorMessage);
			}
			LogHelper.Error(ex2);
		}
		parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", Convert.ToBoolean(strValue));
	}

	public void ReOperate()
	{
	}
}
