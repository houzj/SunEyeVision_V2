using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using Apps.Data;
using Apps.Interface;
using Apps.Localization;
using Apps.XmlParser.ItemInterface;
using Hik.Xml;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class Tab : BaseNode, INotifyPropertyChanged, IName, IDynamicParamSetting, ICanTriggerExecute
{
	[XmlMark(true)]
	public string Name;

	private Hashtable hashtable = new Hashtable();

	public ObservableCollection<object> Categorys;

	public int TabType = 0;

	public int _paramwidth = 76;

	public string ContentTitle;

	public float CustomParamENWidth = 170f;

	private ObservableCollection<object> _Collection = new ObservableCollection<object>();

	private bool isVisible = true;

	public ObservableCollection<object> CategorysA
	{
		get
		{
			return Categorys;
		}
		set
		{
			if (Categorys != value)
			{
				Categorys = value;
				OnPropertyChanged("CategorysA");
			}
		}
	}

	public TabTypeEnum TabTypeA => (TabTypeEnum)TabType;

	public int ParamWidth
	{
		get
		{
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				return 110;
			}
			return 76;
		}
	}

	public string ContentTitleA => ContentTitle;

	public float CustomParamENWidthA => CustomParamENWidth;

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

	public bool IsSelected { get; set; }

	public ObservableCollection<object> Collection
	{
		get
		{
			return _Collection;
		}
		set
		{
			if (_Collection != value)
			{
				_Collection = value;
				OnPropertyChanged("Collection");
			}
		}
	}

	public bool IsVisible
	{
		get
		{
			return isVisible;
		}
		set
		{
			isVisible = value;
			OnPropertyChanged("IsVisible");
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

	public bool CanTriggerExecute { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public override string ToString()
	{
		return Name;
	}

	public IList AllNodes()
	{
		return new ArrayList(hashtable.Values);
	}

	public override BaseNode Init(XmlNode node)
	{
		string text = BaseNode.defaultNameSpace;
		BaseNode.defaultNameSpace = "Apps.XmlParser.ParamTab";
		base.Init(node);
		BaseNode.defaultNameSpace = text;
		InitCollection();
		return this;
	}

	public void InitCollection()
	{
		Collection.Clear();
		if (Categorys == null)
		{
			return;
		}
		hashtable.Clear();
		foreach (Category category in Categorys)
		{
			if (category.Items == null)
			{
				continue;
			}
			foreach (object item in category.Items)
			{
				Collection.Add(item);
				if (!(item is NameValueItem) || (item as NameValueItem).Name == null || hashtable.Contains((item as NameValueItem).Name))
				{
					continue;
				}
				hashtable.Add((item as NameValueItem).Name, item);
				if (!(item is ISubChild) || (item as ISubChild).SubChildren == null)
				{
					continue;
				}
				foreach (object subChild in (item as ISubChild).SubChildren)
				{
					if (subChild is NameValueItem && !hashtable.Contains((subChild as NameValueItem).Name))
					{
						hashtable.Add((subChild as NameValueItem).Name, subChild);
					}
				}
			}
		}
	}

	public BaseNode FindBaseNodeByName(string Name)
	{
		if (hashtable.Contains(Name))
		{
			return hashtable[Name] as BaseNode;
		}
		foreach (IDynamicChild item in hashtable.Values.OfType<IDynamicChild>())
		{
			IList dynamicChild = item.GetDynamicChild();
			foreach (NameValueItem item2 in dynamicChild.OfType<NameValueItem>())
			{
				if (item2 is ISubChild subChild)
				{
					if (subChild.SubChildren == null)
					{
						continue;
					}
					foreach (NameValueItem item3 in subChild.SubChildren.OfType<NameValueItem>())
					{
						if (item3.Name == Name)
						{
							return item3;
						}
					}
				}
				else if (item2.Name == Name)
				{
					return item2;
				}
			}
		}
		return null;
	}

	internal Category FindCategoryByName(string name)
	{
		return CategorysA.OfType<Category>().SingleOrDefault((Category p) => p.NameA == name);
	}

	public void OnXmlLoaded()
	{
		AlgorithmTabRoot algorithmTabRoot = GetTopBaseNode() as AlgorithmTabRoot;
		foreach (object item2 in _Collection)
		{
			if (item2 is NameValueItem nameValueItem)
			{
				nameValueItem.XmlLoadedTask();
				if (item2 is IROISelecter item)
				{
					algorithmTabRoot.ROISelecters.Add(item);
				}
			}
		}
		foreach (object item3 in _Collection)
		{
			if (item3 is NameValueItem)
			{
				(item3 as NameValueItem).NotifyTrigger();
			}
		}
	}

	public void DoIniter()
	{
		if (Collection == null)
		{
			return;
		}
		foreach (NameValueItem item in Collection)
		{
			item.InniterBusiness();
		}
		foreach (NameValueItem item2 in Collection)
		{
			item2.OnInniterBusinessFinish();
		}
	}

	public void DoFinish()
	{
		if (Collection == null)
		{
			return;
		}
		foreach (NameValueItem item in Collection)
		{
			item.OnShownFinish();
		}
	}

	public bool InitItemsValueUnit()
	{
		lock (this)
		{
			foreach (BaseNode item in _Collection)
			{
				if (item is NameValueItem { NeedInit: not false } nameValueItem && nameValueItem.SetParamUnit() == -1)
				{
					return false;
				}
			}
			return true;
		}
	}

	public bool GetItemsValueUnit(bool isGetFromModuleInRemoteMode = false)
	{
		foreach (BaseNode item in _Collection)
		{
			if (item is NameValueItem nameValueItem)
			{
				Debugger.Log(0, "", "VMGetParamName:" + nameValueItem.DisplayName);
				bool isGetFromModule = nameValueItem.IsGetFromModule;
				nameValueItem.IsGetFromModule = isGetFromModuleInRemoteMode;
				if (nameValueItem.GetParamUnit() == -1)
				{
				}
				if (nameValueItem is RadioSelecter)
				{
					nameValueItem.GetUserData();
				}
				nameValueItem.IsGetFromModule = isGetFromModule;
			}
		}
		return true;
	}

	internal bool VisibleChanged()
	{
		return true;
	}

	void IDynamicParamSetting.SetParamTriggerRun()
	{
		IEnumerable<IName> enumerable = from x in Categorys.OfType<IName>()
			where x.Name == "Tab_Run Params" || x.Name == "Tab_ROI Area"
			select x;
		if (enumerable != null)
		{
			enumerable.OfType<IDynamicParamSetting>()?.ToList()?.ForEach((IDynamicParamSetting x) =>
			{
				x.SetParamTriggerRun();
			});
		}
	}
}
