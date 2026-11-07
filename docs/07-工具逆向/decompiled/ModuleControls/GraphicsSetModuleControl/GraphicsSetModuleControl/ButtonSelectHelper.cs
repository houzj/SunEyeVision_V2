using System.Collections.Generic;
using System.Linq;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using Hik.Xml;

namespace GraphicsSetModuleControl;

internal static class ButtonSelectHelper
{
	public static ButtonSelecter GetButtonSelecterFromFront(this Object displayObj, object owner)
	{
		ButtonSelecter buttonSelecter = null;
		BaseNode baseNode = GroupsRoot.GetInstance()["ObjectSubscriptionFromFrontCombinationGroup"];
		if (baseNode is GroupInfoItem)
		{
			GroupInfoItem groupInfoItem = (baseNode as GroupInfoItem).Clone<GroupInfoItem>();
			if (groupInfoItem != null && groupInfoItem.Items != null)
			{
				using IEnumerator<object> enumerator = groupInfoItem.Items.GetEnumerator();
				if (enumerator.MoveNext())
				{
					BaseNode baseNode2 = (BaseNode)enumerator.Current;
					buttonSelecter = baseNode2 as ButtonSelecter;
				}
			}
		}
		string typeA = displayObj.TypeA;
		if (buttonSelecter != null)
		{
			buttonSelecter.SetParentNode(owner);
			buttonSelecter.DropOpenSetter.OperationParams = typeA;
			buttonSelecter.Triggers.OfType<Trigger>().First().Setters.OfType<Setter>().First().OperationParams = displayObj.Name;
			buttonSelecter.Initers.OfType<Setter>().First().OperationParams = displayObj.Name;
			buttonSelecter.InniterBusiness();
			return buttonSelecter;
		}
		return null;
	}
}
