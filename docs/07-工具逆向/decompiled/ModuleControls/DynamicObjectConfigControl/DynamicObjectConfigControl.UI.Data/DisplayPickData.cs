using System;
using System.Linq;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using VMControls.Interface;

namespace DynamicObjectConfigControl.UI.Data;

public class DisplayPickData : PickDataBase
{
	public static readonly string[] allType = (from p in Enum.GetValues(typeof(ObjectTypeEnum)).OfType<ObjectTypeEnum>()
		select p.ToString()).ToArray();

	public override void Init()
	{
		EnumEntrysA = (Node as IVmModuleSet).GetInsideDisplayTree(allType);
	}
}
