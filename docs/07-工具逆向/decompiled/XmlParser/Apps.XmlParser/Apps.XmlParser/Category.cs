using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Apps.Interface;
using Apps.Localization;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;
using Hik.Xml;

namespace Apps.XmlParser;

public class Category : BaseNode, IItemsRoot, INotifyPropertyChanged, IName
{
	public const string arriveTimeLow = "fArrivalTimeStampLow";

	public const string arriveTimeHigh = "fArrivalTimeStampHigh";

	public const string leaveTimeLow = "fLeaveTimeStampLow";

	public const string leaveTimeHigh = "fLeaveTimeStampHigh";

	public const int Out_HiddenFilterCount_Shell = 6;

	public const int Out_HiddenFilterCount_ReadData = 5;

	public const int In_HiddenFilterCount_Shell = 0;

	public const int In_HiddenFilterCount_ReadData = 0;

	[XmlMark(true)]
	public string EHelp;

	[XmlMark(true)]
	public string CHelp;

	[XmlMark(true)]
	public string okcolor;

	[XmlMark(true)]
	public string ngcolor;

	public ObjectCollection Items = new ObjectCollection();

	[XmlMark(true)]
	public string Name;

	[XmlMark(true)]
	public string Description;

	public bool CustomVisible = true;

	private bool isVisible = true;

	public int MaxAddInputFilterCount => 32;

	public int MaxInputFilterCount => 32;

	public ObjectCollection ItemsA
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

	public string NameA
	{
		get
		{
			return Name;
		}
		set
		{
			Name = value;
			OnPropertyChanged("NameA");
		}
	}

	public string DescriptionA
	{
		get
		{
			return Description;
		}
		set
		{
			Description = value;
			OnPropertyChanged("DescriptionA");
		}
	}

	public virtual bool CustomVisibleA
	{
		get
		{
			return CustomVisible;
		}
		set
		{
			if (CustomVisible == value)
			{
				return;
			}
			CustomVisible = value;
			OnPropertyChanged("CustomVisibleA");
			foreach (NameValueItem item in ItemsA.OfType<NameValueItem>())
			{
				item.OnPropertyChanged("GroupName");
			}
		}
	}

	public bool LinePartVisible => (GetParentNode() as Tab).Categorys.IndexOf(this) != 0;

	public virtual string DisplayType => GetType().Name;

	public bool IsVisible
	{
		get
		{
			return isVisible;
		}
		set
		{
			isVisible = value;
		}
	}

	string IName.Name
	{
		get
		{
			return Name;
		}
		set
		{
		}
	}

	[field: XmlIgnore]
	public event PropertyChangedEventHandler PropertyChanged;

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		Items.Parent = this;
		return this;
	}

	internal List<Filter> GetAllFilterByStyle(string ValueType)
	{
		List<Filter> list = new List<Filter>();
		GetFilterByValueType(Items, ValueType, list);
		return list;
	}

	public void GetFilterByValueType(IList subList, string ValueType, List<Filter> allList)
	{
		if (subList == null || allList == null)
		{
			return;
		}
		foreach (object sub in subList)
		{
			if (sub is Filter)
			{
				Filter filter = sub as Filter;
				if (filter.ValueType == ValueType && filter.Visible)
				{
					allList.Add(filter);
				}
			}
			if (sub is Combination)
			{
				Combination combination = sub as Combination;
				if (combination.Filters != null)
				{
					GetFilterByValueType(combination.Filters, ValueType, allList);
				}
			}
		}
	}

	public List<Combination> GetAllCombinationByStyle(string name)
	{
		List<Combination> list = new List<Combination>();
		if (Items != null)
		{
			foreach (object item in Items)
			{
				if (item is Combination)
				{
					GetCombinationByStyle(item as Combination, name, list);
				}
			}
		}
		return list;
	}

	public void GetCombinationByStyle(Combination combination, string name, List<Combination> allList)
	{
		if (combination.Style == name && combination.Visible)
		{
			allList.Add(combination);
		}
		else
		{
			if (combination.Filters == null)
			{
				return;
			}
			foreach (object filter in combination.Filters)
			{
				if (filter is Combination)
				{
					GetCombinationByStyle(filter as Combination, name, allList);
				}
			}
		}
	}

	public Object GetObjectByName(string name)
	{
		if (Items != null)
		{
			foreach (object item in Items)
			{
				if (item is Object && (item as Object).Name == name)
				{
					return item as Object;
				}
			}
		}
		return null;
	}

	public Combination GetCombinationByName(string name, bool LanguageEnable = true)
	{
		if (Items != null)
		{
			foreach (object item in Items)
			{
				if (item is Combination)
				{
					Combination combinationByName = GetCombinationByName(item as Combination, name, LanguageEnable);
					if (combinationByName != null)
					{
						return combinationByName;
					}
				}
			}
		}
		return null;
	}

	public Combination GetCombinationByName(Combination combination, string name, bool LanguageEnable)
	{
		string text = combination.Name;
		if (LanguageEnable)
		{
			text = AppLocalizationService.GetLang(combination.Name);
		}
		if (text == name)
		{
			return combination;
		}
		if (combination.Filters != null)
		{
			foreach (object filter in combination.Filters)
			{
				if (filter is Combination)
				{
					Combination combinationByName = GetCombinationByName(filter as Combination, name, LanguageEnable);
					if (combinationByName != null)
					{
						return combinationByName;
					}
				}
			}
		}
		return null;
	}

	public IList<Filter> GetAllFilters()
	{
		List<Filter> list = new List<Filter>();
		GetFilters(Items, list);
		return list;
	}

	private void GetFilters(IList srcList, List<Filter> retList)
	{
		if (srcList == null || retList == null)
		{
			return;
		}
		foreach (object src in srcList)
		{
			if (src is Filter)
			{
				Filter item = src as Filter;
				retList.Add(item);
			}
			else if (src is Combination)
			{
				Combination combination = src as Combination;
				if (combination.Filters != null)
				{
					GetFilters(combination.Filters, retList);
				}
			}
		}
	}

	object IItemsRoot.GetItemByName(string name)
	{
		if (NameA.ToLower() == "display")
		{
			return ItemsA.OfType<Object>().FirstOrDefault((Object p) => p.NameA == name);
		}
		return GetAllFilters().FirstOrDefault((Filter p) => p.NameA == name);
	}

	public void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	public override string ToString()
	{
		return Name;
	}

	internal bool VisibleChanged()
	{
		bool flag = IsVisible;
		int num = 0;
		foreach (object item in ItemsA)
		{
			if (item is NameValueItem && item is NameValueItem && (item as NameValueItem).CustomVisibleA)
			{
				num++;
			}
		}
		if (((num > 0) & flag) || (num == 0 && !flag))
		{
			return false;
		}
		IsVisible = num > 0;
		return true;
	}
}
