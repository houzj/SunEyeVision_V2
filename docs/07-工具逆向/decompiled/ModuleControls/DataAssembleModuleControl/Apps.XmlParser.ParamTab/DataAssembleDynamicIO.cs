using System.Collections.Generic;
using System.Text;

namespace Apps.XmlParser.ParamTab;

public class DataAssembleDynamicIO : DynamicIO
{
	public override List<string> IoSelectValueTypes => new List<string> { "int", "float", "string" };

	public override string FilterTypeA => "int;float;string";

	public override int SetParamUnit()
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (DynamicIO item in (GetParentNode() as DataAssembleModuleDynamicIO).ItemsA)
		{
			if (item.CustomVisibleA)
			{
				stringBuilder.Append(item.NameA);
				stringBuilder.Append("\r");
			}
		}
		if (string.IsNullOrEmpty(stringBuilder.ToString()))
		{
			return -1;
		}
		int result = 0;
		try
		{
			IoDataSource.Root.Params["SubscribeField"] = stringBuilder.ToString();
		}
		catch
		{
			result = -1;
		}
		return result;
	}
}
