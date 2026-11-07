using System;
using System.Collections.Generic;

namespace Apps.XmlParser.ParamTab;

public class MoveGraspParam : NameValueItem, ISubChild, ISubInfoCollection
{
	public List<object> Items;

	public List<object> ItemsA
	{
		get
		{
			return Items;
		}
		set
		{
			if (Items != value)
			{
				Items = value;
			}
			OnPropertyChanged("ItemsA");
		}
	}

	public IList<object> SubChildren => Items;

	public NameValueItem GetChildByName(string name)
	{
		throw new NotImplementedException();
	}
}
