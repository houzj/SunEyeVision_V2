using Apps.Data;

namespace Apps.XmlParser.ParamTab;

public class PLCItemReceive : BridgeItemBase
{
	public override void OnInniterBusinessFinish()
	{
		if (BridgeObjA is ReadDatasModuleDynamicIO readDatasModuleDynamicIO)
		{
			FunctionCameraManager.Register("GetCommAddress", readDatasModuleDynamicIO.GetCommAddress);
		}
	}
}
