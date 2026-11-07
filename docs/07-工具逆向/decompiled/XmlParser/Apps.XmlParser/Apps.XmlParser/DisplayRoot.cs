using System.Collections.ObjectModel;
using System.Xml;
using Hik.Xml;
using VM.Utility.SingleInstance;

namespace Apps.XmlParser;

public class DisplayRoot : BaseNode
{
	public ObservableCollection<object> Items = new ObservableCollection<object>();

	private static object syncObj = new object();

	public static DisplayRoot instance = null;

	public static DisplayRoot GetInstance()
	{
		lock (syncObj)
		{
			instance = new DisplayRoot();
			instance.loadXml(CurrentPathHelper.Instance.GetCurrentPath() + "\\Module(sp)\\Display.xml");
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
