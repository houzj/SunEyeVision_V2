using System.Collections.Generic;
using System.Linq;
using System.Text;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class AndDynamicIO : DynamicIO
{
	public string DisplayIndex;

	public List<string> SelectValueBasicTypes => new List<string> { "int", "float" };

	public string DisplayIndexA
	{
		get
		{
			return DisplayIndex;
		}
		set
		{
			DisplayIndex = value;
			OnPropertyChanged("DisplayIndexA");
		}
	}

	public override int SetParamUnit()
	{
		AndDynamicIO[] array = (from AndDynamicIO p in (GetParentNode() as AndModuleDynamicIO).ItemsA.Where((object p) => p is AndDynamicIO)
			where p.CustomVisibleA
			select p).ToArray();
		object obj = (GetParentNode(2u) as AlgorithmTabRoot)?.FindBaseNodeByName("LogicalMode");
		if (obj != null)
		{
			object logicType = (obj as EnumerationLogicType).GetLogicType("NOT");
			if (array.Length > 1)
			{
				(logicType as EnumEntry_LogicType).IsEnableA = false;
			}
			else
			{
				(logicType as EnumEntry_LogicType).IsEnableA = true;
			}
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (AndDynamicIO item in (GetParentNode() as DynamicIOContainer<AndDynamicIO>).ItemsA)
		{
			if (item.CustomVisibleA)
			{
				stringBuilder.Append(item.NameA);
				stringBuilder.Append(";");
			}
		}
		if (string.IsNullOrEmpty(stringBuilder.ToString()))
		{
			return -1;
		}
		try
		{
			ServerSDKManager.serverSDKManager.mModuleManager.SetParamValue(IoDataSource.Root.ID, "AndText", stringBuilder.ToString());
		}
		catch
		{
		}
		return 0;
	}
}
