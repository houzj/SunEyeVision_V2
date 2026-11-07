using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using Apps.Interface;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class Category : BaseNode, INotifyPropertyChanged, IName, IDynamicParamSetting, ICanTriggerExecute
{
	[XmlMark(true)]
	public string Name;

	public ObservableCollection<object> Items;

	public bool CustomVisible = true;

	public bool SubscribeVisible = true;

	public bool IsDeleteSubscribe = true;

	public bool CanTriggerRun = false;

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

	public bool LinePartVisible => (GetParentNode() as Tab).Categorys.IndexOf(this) != 0;

	public virtual string DisplayType => GetType().Name;

	public bool IsVisible
	{
		get
		{
			int num = 0;
			foreach (object item in ItemsA)
			{
				if (item is NameValueItem && (item as NameValueItem).CustomVisibleA)
				{
					num++;
				}
			}
			return num > 0;
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

	public bool IsContainDisplayItem => ItemsA != null && ItemsA.OfType<NameValueItem>().FirstOrDefault((NameValueItem p) => p.CustomVisibleA) != null;

	public virtual bool CustomVisibleA
	{
		get
		{
			return CustomVisible;
		}
		set
		{
			if (CustomVisible != value)
			{
				CustomVisible = value;
				OnPropertyChanged("CustomVisibleA");
			}
		}
	}

	public bool SubscribeVisibleA
	{
		get
		{
			return SubscribeVisible;
		}
		set
		{
			if (SubscribeVisible != value)
			{
				SubscribeVisible = value;
				OnPropertyChanged("SubscribeVisibleA");
			}
		}
	}

	public bool IsDeleteSubscribeA
	{
		get
		{
			return IsDeleteSubscribe;
		}
		set
		{
			if (IsDeleteSubscribe != value)
			{
				IsDeleteSubscribe = value;
				OnPropertyChanged("IsDeleteSubscribeA");
			}
		}
	}

	public bool CanTriggerExecute
	{
		get
		{
			return CanTriggerRun;
		}
		set
		{
		}
	}

	[field: XmlIgnore]
	public event PropertyChangedEventHandler PropertyChanged;

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

	void IDynamicParamSetting.SetParamTriggerRun()
	{
	}
}
