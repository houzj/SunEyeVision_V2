using Apps.Data;

namespace VisionMaster.AppMainWindow;

public class AccessInfo : NotifyPropertyChangedBaseNode
{
	private string _UserName;

	private string _UserDisPlayName;

	public string UserName
	{
		get
		{
			return _UserName;
		}
		set
		{
			if (_UserName != value)
			{
				_UserName = value;
				OnPropertyChanged("UserName");
			}
		}
	}

	public string UserDisPlayName
	{
		get
		{
			return _UserDisPlayName;
		}
		set
		{
			if (_UserDisPlayName != value)
			{
				_UserDisPlayName = value;
				OnPropertyChanged("UserDisPlayName");
			}
		}
	}
}
