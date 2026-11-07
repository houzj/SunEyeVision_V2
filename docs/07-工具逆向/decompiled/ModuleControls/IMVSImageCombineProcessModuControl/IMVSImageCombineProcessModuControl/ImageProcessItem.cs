using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using VM.Utility;

namespace IMVSImageCombineProcessModuControl;

public class ImageProcessItem : INotifyPropertyChanged
{
	private bool _ispopuopen;

	private bool _valuechanged;

	private DelegateCommand _delegateCommand;

	private DelegateCommand _moveUpCommand;

	private DelegateCommand _moveDownCommand;

	private ImageProcessCategory _category;

	private ObservableCollection<ImageProcessItem> _collection;

	private bool _enable;

	private string _name;

	public bool IsPopuOpen
	{
		get
		{
			return _ispopuopen;
		}
		set
		{
			_ispopuopen = value;
			OnPropertyChanged("IsPopuOpen");
			if (!_ispopuopen && _valuechanged && ValueChanged != null)
			{
				ValueChanged(this, new EventArgs());
			}
			_valuechanged = false;
		}
	}

	public IMAGEPROCESS_SINGLE_INFO IMAGEPROCESS_SINGLE_INFO
	{
		get
		{
			IMAGEPROCESS_SINGLE_INFO result = new IMAGEPROCESS_SINGLE_INFO
			{
				nTypeIndex = Category.TypeIndex,
				nReserved = new int[4]
			};
			List<NameValueItem> nameValueItems = Category.GetNameValueItems();
			result.nParamCount = nameValueItems.Count;
			result.stParamInfo = new IMAGEPROCESS_PARAM_INFO[32];
			for (int i = 0; i < nameValueItems.Count; i++)
			{
				result.stParamInfo[i].nAlgorithmIndex = Convert.ToInt32(nameValueItems[i].AlgorithmIndex, 16);
				result.stParamInfo[i].chszValue = nameValueItems[i].Value;
			}
			result.bEnabled = Enable;
			return result;
		}
	}

	public ImageProcessCategory Category
	{
		get
		{
			return _category;
		}
		set
		{
			if (_category != null)
			{
				foreach (NameValueItem nameValueItem in _category.GetNameValueItems())
				{
					nameValueItem.PropertyChanged -= OnValueChanged;
				}
			}
			_category = value;
			OnPropertyChanged("Category");
			if (_category == null)
			{
				return;
			}
			foreach (NameValueItem nameValueItem2 in _category.GetNameValueItems())
			{
				nameValueItem2.PropertyChanged += OnValueChanged;
			}
		}
	}

	public int Index { get; set; }

	public ObservableCollection<ImageProcessItem> Parent
	{
		get
		{
			return _collection;
		}
		set
		{
			if (_collection != null)
			{
				_collection.CollectionChanged -= OnCollectionChanged;
			}
			_collection = value;
			if (_collection != null)
			{
				_collection.CollectionChanged += OnCollectionChanged;
			}
		}
	}

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			_name = value;
			OnPropertyChanged("Name");
			OnPropertyChanged("DisplayName");
		}
	}

	public string DisplayName => AppLocalizationService.GetLang(Name);

	public bool Enable
	{
		get
		{
			return _enable;
		}
		set
		{
			_enable = value;
			OnPropertyChanged("Enable");
			if (ValueChanged != null)
			{
				ValueChanged(this, new EventArgs());
			}
		}
	}

	public bool MoveUpEnable
	{
		get
		{
			if (Parent == null)
			{
				return false;
			}
			return Index - 1 > 0;
		}
	}

	public bool MoveDownEnable
	{
		get
		{
			if (Parent == null)
			{
				return false;
			}
			return Index < Parent.Count;
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			return _delegateCommand;
		}
		set
		{
			_delegateCommand = value as DelegateCommand;
			OnPropertyChanged("DeleteCommand");
		}
	}

	public ICommand MoveUpCommand
	{
		get
		{
			return _moveUpCommand;
		}
		set
		{
			_moveUpCommand = value as DelegateCommand;
			OnPropertyChanged("MoveUpCommand");
		}
	}

	public ICommand MoveDownCommand
	{
		get
		{
			return _moveDownCommand;
		}
		set
		{
			_moveDownCommand = value as DelegateCommand;
			OnPropertyChanged("MoveDownCommand");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event EventHandler ValueChanged;

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	public void Init(ImageProcessCategory category, ObservableCollection<ImageProcessItem> parent)
	{
		Category = category;
		Parent = parent;
		Parent.Add(this);
	}

	public void Delete()
	{
		Parent.Remove(this);
		Category = null;
		Parent = null;
		if (ValueChanged != null)
		{
			Delegate[] invocationList = ValueChanged.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				ValueChanged -= (EventHandler)obj;
			}
			ValueChanged = null;
		}
	}

	public void MoveDown()
	{
		if (MoveDownEnable)
		{
			int index = Index - 1;
			ImageProcessItem item = Parent[Index];
			Parent.RemoveAt(Index);
			Parent.Insert(index, item);
		}
	}

	public void MoveUp()
	{
		if (MoveUpEnable)
		{
			int num = Index - 1;
			int index = num - 1;
			ImageProcessItem item = Parent[num];
			Parent.RemoveAt(num);
			Parent.Insert(index, item);
		}
	}

	private void OnValueChanged(object sender, PropertyChangedEventArgs e)
	{
		if (!_valuechanged)
		{
			_valuechanged = true;
		}
	}

	private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (Parent != null && Parent.Contains(this))
		{
			Index = Parent.IndexOf(this) + 1;
			OnPropertyChanged("Index");
			OnPropertyChanged("MoveUpEnable");
			OnPropertyChanged("MoveDownEnable");
		}
	}

	public ImageProcessItem()
	{
		DeleteCommand = new DelegateCommand(Delete);
		MoveDownCommand = new DelegateCommand(MoveDown);
		MoveUpCommand = new DelegateCommand(MoveUp);
	}
}
