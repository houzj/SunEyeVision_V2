namespace Apps.XmlParser.ParamTab;

public class ProtocolOutputParam_Byte : BridgeItemBase
{
	public override int GetParamUnit()
	{
		int result = 0;
		if (BridgeObjA != null)
		{
			result = (BridgeObjA as DataAnalysisModuleDynamicIO).InitByteData();
		}
		return result;
	}
}
