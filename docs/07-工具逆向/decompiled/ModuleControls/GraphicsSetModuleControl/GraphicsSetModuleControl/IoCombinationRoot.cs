using System.Collections.Generic;
using System.Xml;
using Hik.Xml;
using VM.Utility.SingleInstance;

namespace GraphicsSetModuleControl;

public class IoCombinationRoot : BaseNode
{
	public List<object> Items;

	private static IoCombinationRoot instance = null;

	public static IoCombinationRoot GetInstance()
	{
		if (instance == null)
		{
			instance = new IoCombinationRoot();
			instance.loadXml(CurrentPathHelper.Instance.GetCurrentPath() + "\\Module(sp)\\x64\\Logic\\GraphicsSetModule\\IoCombination.xml");
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
