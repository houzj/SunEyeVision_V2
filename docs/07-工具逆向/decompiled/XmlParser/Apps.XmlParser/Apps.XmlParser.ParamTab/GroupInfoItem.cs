using System;
using System.Collections.ObjectModel;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class GroupInfoItem : BaseNode, IName
{
	[XmlMark(true)]
	public string Name;

	public ObservableCollection<object> Items;

	string IName.Name
	{
		get
		{
			return Name;
		}
		set
		{
			throw new NotImplementedException();
		}
	}
}
