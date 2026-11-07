using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Apps.Localization;
using Hik.Xml;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class EnumEntry : BaseNode, INotifyPropertyChanged
{
	public delegate void ItemClickHandler(object param);

	public string Description;

	public string DisplayName;

	public int Value;

	public string StrValue;

	public bool IsVisible = true;

	public ObservableCollection<object> EnumEntrys;

	[XmlMark(true)]
	public string Name;

	[XmlMark(true)]
	public string NameSpace;

	private DelegateCommand<object> _ClickCommand = null;

	private int _IoNum;

	public int DeviceType { get; set; }

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

	public int ValueA
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

	public string StrValueA
	{
		get
		{
			return StrValue;
		}
		set
		{
			StrValue = value;
			OnPropertyChanged("StrValueA");
		}
	}

	public ICommand OnItemClickCommand
	{
		get
		{
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand<object>((Action<object>)ClickCmdExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	public string FullName
	{
		get
		{
			string text = DisplayNameA;
			object obj = GetParentNode();
			while (obj != null)
			{
				if (obj != null && obj is EnumEntry)
				{
					text = (obj as EnumEntry).DisplayNameA + "." + text;
					obj = (obj as EnumEntry).GetParentNode();
				}
			}
			return text;
		}
	}

	public int IoNum
	{
		get
		{
			return _IoNum;
		}
		set
		{
			_IoNum = value;
			OnPropertyChanged("IoNum");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event ItemClickHandler OnItemClicked;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public void ClickCmdExecute(object e)
	{
		if (OnItemClicked != null)
		{
			OnItemClicked(e);
		}
	}

	public override string ToString()
	{
		return DisplayName;
	}
}
