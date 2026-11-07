using System;

namespace Apps.XmlParser.ParamTab;

public class ProtocolOutputParam_Shell : BridgeItemBase
{
	public void ContinueRunExcute()
	{
		throw new NotImplementedException();
	}

	public object ExecuteFunction(object obj = null)
	{
		BridgeObjA = null;
		OnPropertyChanged("BridgeObjA");
		return null;
	}

	public void OkExcute()
	{
		throw new NotImplementedException();
	}

	public void ResetExcute()
	{
		throw new NotImplementedException();
	}

	public void SelfRunExcute()
	{
		throw new NotImplementedException();
	}
}
