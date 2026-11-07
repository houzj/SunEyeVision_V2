using System.Collections.ObjectModel;
using System.ComponentModel;

namespace IMVSOcrModuControl;

public class OCRTrainUIData : INotifyPropertyChanged
{
	public ObservableCollection<object> _ItemsData = new ObservableCollection<object>();

	public ObservableCollection<CharacterItemData> _Collection = new ObservableCollection<CharacterItemData>();

	public ObservableCollection<object> ItemsData
	{
		get
		{
			return _ItemsData;
		}
		set
		{
			if (_ItemsData != value)
			{
				_ItemsData = value;
				NotifyPropertyChanged("ItemsData");
			}
		}
	}

	public ObservableCollection<CharacterItemData> Collection
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
				NotifyPropertyChanged("Collection");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public int GetAllDataCount()
	{
		int num = Collection.Count;
		foreach (CharacterItemData item in Collection)
		{
			if (item != null)
			{
				num += item.Collection.Count;
			}
		}
		return num;
	}
}
