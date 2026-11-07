using System.ComponentModel;
using Apps.BaseData;
using VMControls.RenderInterface;

namespace Apps.XmlParser;

public class ROIObservableRangeCollection<T> : ObservableRangeCollection<T>, IROIUserType, IROIVisible
{
	private ROIUseType _UseType;

	private bool _IsVisible;

	public ROIUseType UseType
	{
		get
		{
			return _UseType;
		}
		set
		{
			if (_UseType != value)
			{
				_UseType = value;
				OnPropertyChanged("UseType");
			}
		}
	}

	bool IROIVisible.IsVisible
	{
		get
		{
			return _IsVisible;
		}
		set
		{
			if (_IsVisible != value)
			{
				_IsVisible = value;
				OnPropertyChanged("IsVisible");
			}
		}
	}

	public void OnPropertyChanged(string name)
	{
		OnPropertyChanged(new PropertyChangedEventArgs(name));
	}
}
