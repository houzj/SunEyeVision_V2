using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Data;
using Mvvm.Commands;

namespace OCRModuleCommonControl;

public class MultiFilterData : NotifyPropertyChangedBase
{
	private ObservableCollection<SingleFilterData> filterDataCollection = new ObservableCollection<SingleFilterData>();

	private SingleFilterData curSelectItem;

	private bool isSingleCheck;

	private DelegateCommand<object> deleteCommand = null;

	private DelegateCommand<object> upCommand = null;

	private DelegateCommand<object> downCommand = null;

	public bool IsROIListCustomizable { get; set; } = false;

	public ObservableCollection<SingleFilterData> FilterDataCollection => filterDataCollection;

	public SingleFilterData CurSelectItem
	{
		get
		{
			return curSelectItem;
		}
		set
		{
			if (curSelectItem != value)
			{
				curSelectItem = value;
				OnPropertyChanged("CurSelectItem");
				curSelectItem.OnPropertyChanged("OCRFilerUIData");
				((NotifyPropertyChangedBase)(object)curSelectItem.OCRFilerUIData).OnPropertyChanged("Collection");
			}
		}
	}

	public bool IsSingleCheck
	{
		get
		{
			return isSingleCheck;
		}
		set
		{
			if (isSingleCheck != value)
			{
				isSingleCheck = value;
				OnPropertyChanged("IsSingleCheck");
			}
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			if (deleteCommand == null)
			{
				deleteCommand = new DelegateCommand<object>((Action<object>)DeleteCommandExcute);
			}
			return (ICommand)deleteCommand;
		}
	}

	public ICommand UpCommand
	{
		get
		{
			if (upCommand == null)
			{
				upCommand = new DelegateCommand<object>((Action<object>)UpCommandExcute);
			}
			return (ICommand)upCommand;
		}
	}

	public ICommand DownCommand
	{
		get
		{
			if (downCommand == null)
			{
				downCommand = new DelegateCommand<object>((Action<object>)DownCommandExcute);
			}
			return (ICommand)downCommand;
		}
	}

	private void DeleteCommandExcute(object param)
	{
		if (param is SingleFilterData item && FilterDataCollection.Remove(item) && FilterDataCollection.Count > 0)
		{
			RefreshIndex();
			CurSelectItem = FilterDataCollection[0];
		}
	}

	private void UpCommandExcute(object param)
	{
		if (param is SingleFilterData item)
		{
			int num = FilterDataCollection.IndexOf(item);
			if (num > 0)
			{
				FilterDataCollection.Remove(item);
				FilterDataCollection.Insert(num - 1, item);
				RefreshIndex();
				CurSelectItem = item;
			}
		}
	}

	private void DownCommandExcute(object param)
	{
		if (param is SingleFilterData item)
		{
			int num = FilterDataCollection.IndexOf(item);
			if (num >= 0 && num < FilterDataCollection.Count - 1)
			{
				FilterDataCollection.Remove(item);
				FilterDataCollection.Insert(num + 1, item);
				RefreshIndex();
				CurSelectItem = item;
			}
		}
	}

	private void RefreshIndex()
	{
		int count = FilterDataCollection.Count;
		for (int i = 0; i < count; i++)
		{
			FilterDataCollection[i].Index = i + 1;
		}
	}
}
