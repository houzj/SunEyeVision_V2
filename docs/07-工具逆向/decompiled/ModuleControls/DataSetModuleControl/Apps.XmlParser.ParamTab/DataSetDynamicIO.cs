using System.Collections.Generic;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class DataSetDynamicIO : DynamicIO
{
	public List<string> SelectValueBasicTypes => new List<string> { "int", "float", "string" };

	public override int SetParamUnit()
	{
		int num = 0;
		foreach (DataSetDynamicIO item in (GetParentNode() as DataSetModuleDynamicIO)?.ItemsA)
		{
			if (item.CustomVisibleA)
			{
				ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(IoDataSource.Root.ID, "var" + num, item.NameA);
				ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(IoDataSource.Root.ID, "type" + num, item.ValueTypeA);
				num++;
			}
		}
		ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(IoDataSource.Root.ID, "DataCount", num.ToString());
		return 0;
	}
}
