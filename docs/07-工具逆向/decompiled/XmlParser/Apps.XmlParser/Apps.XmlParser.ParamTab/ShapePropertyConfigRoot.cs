using System.Collections.ObjectModel;
using System.Xml;
using Hik.Xml;
using VM.Utility.SingleInstance;

namespace Apps.XmlParser.ParamTab;

public class ShapePropertyConfigRoot : BaseNode
{
	public ObservableCollection<object> Items = new ObservableCollection<object>();

	private static object syncObj = new object();

	public static ShapePropertyConfigRoot instance = null;

	public static ShapePropertyConfigRoot GetInstance()
	{
		lock (syncObj)
		{
			instance = new ShapePropertyConfigRoot();
			instance.loadXml(CurrentPathHelper.Instance.GetCurrentPath() + "\\Module(sp)\\ShapePropertyConfig.xml");
		}
		return instance;
	}

	public override BaseNode Init(XmlNode node)
	{
		string text = BaseNode.defaultNameSpace;
		BaseNode.defaultNameSpace = "Apps.XmlParser.ParamTab";
		base.Init(node);
		BaseNode.defaultNameSpace = text;
		return this;
	}
}
