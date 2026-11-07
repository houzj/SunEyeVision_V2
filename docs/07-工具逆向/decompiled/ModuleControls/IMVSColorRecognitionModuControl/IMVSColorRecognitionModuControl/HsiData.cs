using System.ComponentModel;
using System.Windows.Media;

namespace IMVSColorRecognitionModuControl;

public class HsiData : INotifyPropertyChanged
{
	private string _Name;

	private PointCollection _HData;

	private PointCollection _SData;

	private PointCollection _IData;

	public string Name
	{
		get
		{
			return _Name;
		}
		set
		{
			if (_Name != value)
			{
				_Name = value;
				NotifyPropertyChanged("Name");
			}
		}
	}

	public PointCollection HData
	{
		get
		{
			return _HData;
		}
		set
		{
			if (value != _HData)
			{
				_HData = value;
				NotifyPropertyChanged("HData");
			}
		}
	}

	public PointCollection SData
	{
		get
		{
			return _SData;
		}
		set
		{
			if (value != _SData)
			{
				_SData = value;
				NotifyPropertyChanged("SData");
			}
		}
	}

	public PointCollection IData
	{
		get
		{
			return _IData;
		}
		set
		{
			if (value != _IData)
			{
				_IData = value;
				NotifyPropertyChanged("IData");
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
