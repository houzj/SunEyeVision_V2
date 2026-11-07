using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Apps.Interface;
using Hik.Xml;

namespace Apps.XmlParser;

public class Combination : ParamItem, IAccessMode, ISelectable
{
	[XmlMark(true)]
	public string Style;

	[XmlMark(true)]
	public string AccessMode;

	[XmlMark(true)]
	public bool Visible = true;

	public ObservableCollection<object> Filters;

	private bool _HasRelative;

	public string StyleA
	{
		get
		{
			return Style;
		}
		set
		{
			Style = value;
			OnPropertyChanged("StyleA");
		}
	}

	public string accessMode
	{
		get
		{
			return AccessMode;
		}
		set
		{
			AccessMode = value;
		}
	}

	public string AccessModeA
	{
		get
		{
			return AccessMode;
		}
		set
		{
			AccessMode = value;
			OnPropertyChanged("AccessModeA");
		}
	}

	public ObservableCollection<object> FiltersA
	{
		get
		{
			return Filters;
		}
		set
		{
			Filters = value;
			OnPropertyChanged("FiltersA");
		}
	}

	public override string ParamValueType => Style;

	public override bool HasRelative
	{
		get
		{
			_HasRelative = Child_HasRelative(Filters);
			return _HasRelative;
		}
		set
		{
			_HasRelative = value;
			OnPropertyChanged("HasRelative");
			object obj = GetParentNode();
			if (_HasRelative && obj != null && obj is Combination)
			{
				(obj as Combination).OnPropertyChanged("HasRelative");
			}
		}
	}

	private bool Child_HasRelative(ObservableCollection<object> filters)
	{
		if (filters != null)
		{
			foreach (object filter in filters)
			{
				if (filter is Filter)
				{
					if ((filter as Filter).HasRelative)
					{
						return (filter as Filter).HasRelative;
					}
				}
				else if (Child_HasRelative((filter as Combination).Filters))
				{
					return true;
				}
			}
			return false;
		}
		return false;
	}

	public string GetRelativeStringIndex()
	{
		string text = null;
		if (Filters != null)
		{
			foreach (object filter in Filters)
			{
				if (filter is Filter)
				{
					if (text == null)
					{
						text = (filter as Filter).RelativeIndexStringName;
					}
					else if (text != (filter as Filter).RelativeIndexStringName)
					{
						return "null";
					}
				}
				else if (filter is Combination)
				{
					if (text == null)
					{
						text = (filter as Combination).GetRelativeStringIndex();
					}
					else if (text != (filter as Combination).GetRelativeStringIndex())
					{
						return "null";
					}
				}
			}
			return text;
		}
		return null;
	}

	internal Filter GetOneFilter()
	{
		if (Filters != null)
		{
			foreach (object filter in Filters)
			{
				if (filter is Filter)
				{
					return filter as Filter;
				}
			}
			return null;
		}
		return null;
	}

	public List<Filter> GetAllFilters()
	{
		List<Filter> list = new List<Filter>();
		GetAllFilters(Filters, list);
		return list;
	}

	private void GetAllFilters(IList list, IList allList)
	{
		if (list == null)
		{
			return;
		}
		foreach (object item in list)
		{
			if (item is Filter)
			{
				allList.Add(item);
			}
			else if (item is Combination)
			{
				GetAllFilters((item as Combination).Filters, allList);
			}
		}
	}

	public List<Combination> GetAllCombination(List<Combination> ls = null)
	{
		if (ls == null)
		{
			ls = new List<Combination>();
		}
		foreach (object filter in Filters)
		{
			if (filter is Combination combination)
			{
				ls.Add(combination);
				combination.GetAllCombination(ls);
			}
		}
		return ls;
	}
}
