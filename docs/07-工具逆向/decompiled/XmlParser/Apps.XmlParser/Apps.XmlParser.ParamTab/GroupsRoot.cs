using System.Collections;
using System.Collections.ObjectModel;
using System.Xml;
using Hik.Xml;
using VM.Utility.SingleInstance;

namespace Apps.XmlParser.ParamTab;

public class GroupsRoot : BaseNode
{
	private Hashtable hashtable = new Hashtable();

	private static object syncObj = new object();

	public static GroupsRoot instance = null;

	public ObservableCollection<object> Items;

	public BaseNode this[string name] => FindNode(name);

	public static GroupsRoot GetInstance()
	{
		lock (syncObj)
		{
			if (instance == null)
			{
				instance = new GroupsRoot();
			}
			return instance;
		}
	}

	private GroupsRoot()
	{
		BaseNode.defaultNameSpace = "Apps.XmlParser.ParamTab";
		defaultFileName = CurrentPathHelper.Instance.GetCurrentPath() + "\\Module(sp)\\Group.xml";
		try
		{
			loadXml();
		}
		catch
		{
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		InitItems();
		return this;
	}

	private void InitItems()
	{
		if (Items == null)
		{
			return;
		}
		foreach (object item in Items)
		{
			if (item is IName && (item as IName).Name != null && !hashtable.Contains((item as IName).Name))
			{
				hashtable.Add((item as IName).Name, item);
			}
		}
	}

	private BaseNode FindNode(string name)
	{
		if (hashtable.ContainsKey(name))
		{
			return hashtable[name] as BaseNode;
		}
		return null;
	}
}
