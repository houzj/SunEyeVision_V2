using System.Collections.ObjectModel;
using System.ComponentModel;
using Hik.Xml;

namespace IMVSColorRecognitionModuControl;

public class ImagePageData : BaseNode, INotifyPropertyChanged, IItemsData
{
	public ObservableCollection<object> _ItemsData = new ObservableCollection<object>();

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

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
