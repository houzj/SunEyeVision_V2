using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Apps.XmlParser.ParamTab;
using VM.Core;

namespace DynamicObjectConfigControl.UI.Data;

public class RunParamPickData : PickDataBase
{
	public GroupRunParamConfigData ConfigData = null;

	public List<string> FilterTypes { get; set; }

	public string SearchWord { get; set; }

	public override void Init()
	{
		(Node as VmModule).SetConfigData(ConfigData);
		EnumEntrysA = (Node as VmModule).GetInsideRunParamTree(FilterTypes, SearchWord);
	}

	protected override void GetLeafNode(List<object> leafNodes, IList ls)
	{
		foreach (Holder item2 in ls.OfType<Holder>())
		{
			if (item2.HolderTarget is NameValueItem item && item2.OutputEnabled)
			{
				leafNodes.Add(item);
			}
			else
			{
				GetLeafNode(leafNodes, item2.EnumEntrysA);
			}
		}
	}
}
