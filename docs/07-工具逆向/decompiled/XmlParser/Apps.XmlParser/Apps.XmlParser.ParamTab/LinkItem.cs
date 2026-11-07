using System;
using System.Collections;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class LinkItem : BaseNode
{
	[XmlMark(true)]
	public string LinkName;

	[XmlMark(true)]
	public bool CustomVisible = true;

	private IList tempList;

	public override void AfterAddedToArray(IList list)
	{
		if (list != null)
		{
			base.AfterAddedToArray(list);
			tempList = list;
			if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				algorithmTabRoot.XmlLoaded += root_XmlLoaded;
			}
		}
	}

	private void root_XmlLoaded(object sender, EventArgs e)
	{
		if (!(sender is AlgorithmTabRoot algorithmTabRoot))
		{
			return;
		}
		algorithmTabRoot.XmlLoaded -= root_XmlLoaded;
		if (!(algorithmTabRoot.FindBaseNodeByName(LinkName) is NameValueItem nameValueItem) || tempList == null || !(nameValueItem.Clone() is NameValueItem nameValueItem2))
		{
			return;
		}
		int num = tempList.IndexOf(this);
		if (num >= 0)
		{
			tempList.Remove(this);
			nameValueItem2.CustomVisible = CustomVisible;
			tempList.Insert(num, nameValueItem2);
			tempList = null;
			nameValueItem2.SetParentNode(GetParentNode());
		}
		Tab tab = GetParentNode<Tab>();
		if (tab != null)
		{
			num = tab.Collection.IndexOf(this);
			if (num >= 0)
			{
				tab.Collection.Remove(this);
				tab.Collection.Insert(num, nameValueItem2);
			}
		}
	}
}
