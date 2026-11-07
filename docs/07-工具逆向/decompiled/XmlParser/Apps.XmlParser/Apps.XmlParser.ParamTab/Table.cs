using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Apps.XmlParser.ParamTab;

public class Table : NameValueItem, ISubChild, ISubInfoCollection
{
	public ObservableCollection<object> Columns;

	public bool IsAddDelEnable = true;

	public string DataName;

	public string CommandName;

	public ObservableCollection<object> ColumnsA
	{
		get
		{
			return Columns;
		}
		set
		{
			if (Columns != value)
			{
				Columns = value;
			}
			OnPropertyChanged("ColumnsA");
		}
	}

	public bool IsAddDelEnableA
	{
		get
		{
			return IsAddDelEnable;
		}
		set
		{
			IsAddDelEnable = value;
			OnPropertyChanged("IsAddDelEnableA");
		}
	}

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

	public IList<object> SubChildren => Columns;

	public NameValueItem GetChildByName(string name)
	{
		throw new NotImplementedException();
	}
}
