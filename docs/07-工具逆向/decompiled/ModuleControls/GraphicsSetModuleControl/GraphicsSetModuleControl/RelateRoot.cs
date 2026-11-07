using System.Collections.ObjectModel;
using System.Xml;
using Hik.Xml;
using VM.Utility.SingleInstance;

namespace GraphicsSetModuleControl;

public class RelateRoot : BaseNode
{
	public ObservableCollection<object> Items = new ObservableCollection<object>();

	private static object syncObj = new object();

	public static RelateRoot instance = null;

	public static RelateRoot GetInstance()
	{
		lock (syncObj)
		{
			instance = new RelateRoot();
			instance.loadXml(CurrentPathHelper.Instance.GetCurrentPath() + "\\Module(sp)\\x64\\Logic\\GraphicsSetModule\\Relate.xml");
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
