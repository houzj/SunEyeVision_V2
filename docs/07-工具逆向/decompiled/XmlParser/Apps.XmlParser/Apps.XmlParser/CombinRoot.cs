using System.Collections.Generic;
using System.Xml;
using Hik.Xml;
using VM.Utility.SingleInstance;

namespace Apps.XmlParser;

public class CombinRoot : BaseNode
{
	public List<object> Items;

	private static CombinRoot instance = null;

	public static CombinRoot GetInstance()
	{
		if (instance == null)
		{
			instance = new CombinRoot();
			instance.loadXml(CurrentPathHelper.Instance.GetCurrentPath() + "\\Module(sp)\\Combination.xml");
		}
		return instance;
	}

	public override BaseNode Init(XmlNode node)
	{
		string text = BaseNode.defaultNameSpace;
		BaseNode.defaultNameSpace = "Apps.XmlParser";
		base.Init(node);
		BaseNode.defaultNameSpace = text;
		return this;
	}
}
