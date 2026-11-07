using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Apps.XmlParser.ParamTab;

public class ParamGroup : NameValueItem, ISubChild, ISubInfoCollection
{
	public string DataName;

	public string CommandName;

	public ObservableCollection<object> Items;

	public bool AddEnable = true;

	public string DataNameA
	{
		get
		{
			return DataName;
		}
		set
		{
			DataName = value;
			OnPropertyChanged("DataNameA");
		}
	}

	public string CommandNameA
	{
		get
		{
			return CommandName;
		}
		set
		{
			CommandName = value;
			OnPropertyChanged("CommandNameA");
		}
	}

	public ObservableCollection<object> ItemsA
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

	public bool AddEnableA
	{
		get
		{
			return AddEnable;
		}
		set
		{
			AddEnable = value;
			OnPropertyChanged("AddEnableA");
		}
	}

	public IList<object> SubChildren => Items;

	public NameValueItem GetChildByName(string name)
	{
		throw new NotImplementedException();
	}
}
