using System.Collections;
using System.Collections.ObjectModel;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class GroupLinkItem : NameValueItem
{
	public string LinkName;

	public ObservableCollection<object> Items;

	public override void AfterAddedToArray(IList list)
	{
		if (list == null)
		{
			return;
		}
		base.AfterAddedToArray(list);
		list.Remove(this);
		BaseNode baseNode = GroupsRoot.GetInstance()[LinkName];
		if (!(baseNode is GroupInfoItem))
		{
			return;
		}
		GroupInfoItem groupInfoItem = (baseNode as GroupInfoItem).Clone<GroupInfoItem>();
		if (groupInfoItem == null || groupInfoItem.Items == null)
		{
			return;
		}
		foreach (BaseNode item in groupInfoItem.Items)
		{
			list.Add(item);
			item.SetParentNode(GetParentNode());
		}
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}
}
