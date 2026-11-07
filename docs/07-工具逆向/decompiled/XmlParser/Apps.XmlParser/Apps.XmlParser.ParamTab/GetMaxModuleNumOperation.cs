using System;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class GetMaxModuleNumOperation : IOperation
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

	public GetMaxModuleNumOperation(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		NameValueItem parentNode = Executor.GetParentNode<NameValueItem>();
		string comName = parentNode.ComName;
		if (ExtraParams != null && ExtraParams.Length != 0)
		{
			comName = ExtraParams[0];
		}
		uint maxModuleNum = ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetMaxModuleNum();
		parentNode.SetPropertyValueByNameNotrigger(TargetName + "A", Convert.ToInt32(maxModuleNum));
	}

	public void ReOperate()
	{
	}
}
