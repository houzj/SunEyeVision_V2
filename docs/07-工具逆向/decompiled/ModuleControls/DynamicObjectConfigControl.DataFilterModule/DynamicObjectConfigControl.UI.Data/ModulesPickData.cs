using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Apps.XmlParser.ParamTab;
using VM.Core;
using VMControls.Interface;

namespace DynamicObjectConfigControl.UI.Data;

public class ModulesPickData : PickDataBase
{
	public string StrModuleName { get; set; }

	public string ParamName { get; internal set; }

	public string ParamDisplayName { get; internal set; }

	public override void Init()
	{
		EnumEntrysA = (Node as VmModuleSet).GetParamsOfEqualTypeTree(new string[3] { StrModuleName, ParamName, ParamDisplayName });
	}

	protected override void GetLeafNode(List<object> leafNodes, IList ls)
	{
		foreach (Holder item in ls.OfType<Holder>())
		{
			if (item.HolderTarget is IVmModuleSet)
			{
				GetLeafNode(leafNodes, item.EnumEntrysA);
			}
			else
			{
				leafNodes.Add(item.HolderTarget);
			}
		}
	}
}
