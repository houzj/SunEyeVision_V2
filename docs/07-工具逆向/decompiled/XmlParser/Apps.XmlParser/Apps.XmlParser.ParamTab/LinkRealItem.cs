using System;
using System.Xml;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class LinkRealItem : BaseNode
{
	public string LinkName;

	public string TargetPropertyName;

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			algorithmTabRoot.XmlLoaded += root_XmlLoaded;
		}
		return this;
	}

	private void root_XmlLoaded(object sender, EventArgs e)
	{
		if (sender is AlgorithmTabRoot algorithmTabRoot)
		{
			algorithmTabRoot.XmlLoaded -= root_XmlLoaded;
			NameValueItem nameValueItem = algorithmTabRoot.FindBaseNodeByName(LinkName) as NameValueItem;
			NameValueItem nameValueItem2 = GetParentNode<NameValueItem>();
			if (nameValueItem != null)
			{
				nameValueItem2?.SetPropertyValueByName(TargetPropertyName + "A", nameValueItem);
			}
		}
	}
}
