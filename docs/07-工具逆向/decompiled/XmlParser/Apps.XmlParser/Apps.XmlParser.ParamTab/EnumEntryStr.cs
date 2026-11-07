using System.Collections.ObjectModel;
using System.ComponentModel;
using Apps.Localization;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class EnumEntryStr : BaseNode, INotifyPropertyChanged
{
	public string Description;

	public string DisplayName;

	public string Value;

	public bool IsVisible = true;

	public ObservableCollection<object> EnumEntrys;

	[XmlMark(true)]
	public string Name;

	[XmlMark(true)]
	public string NameSpace;

	public object ReservedProperty { get; set; }

	public ObservableCollection<object> EnumEntrysA
	{
		get
		{
			return EnumEntrys;
		}
		set
		{
			if (EnumEntrys != value)
			{
				EnumEntrys = value;
			}
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

	public bool IsVisibleA
	{
		get
		{
			return IsVisible;
		}
		set
		{
			IsVisible = value;
			OnPropertyChanged("IsVisibleA");
		}
	}

	public string NameSpaceA
	{
		get
		{
			return NameSpace;
		}
		set
		{
			NameSpace = value;
			OnPropertyChanged("NameSpaceA");
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
		}
	}

	public string DisplayNameA
	{
		get
		{
			return DisplayName;
		}
		set
		{
			DisplayName = value;
		}
	}

	public string DisplayNameALang => AppLocalizationService.GetLang(DisplayName);

	public string ValueA
	{
		get
		{
			return Value;
		}
		set
		{
			Value = value;
			OnPropertyChanged("ValueA");
		}
	}

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
		return DisplayName;
	}
}
