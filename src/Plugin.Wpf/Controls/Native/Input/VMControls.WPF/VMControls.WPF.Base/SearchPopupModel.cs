using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Base;

public class SearchPopupModel : INotifyPropertyChanged
{
	private string _id = string.Empty;

	private string _name = string.Empty;

	private string _fullPath = string.Empty;

	private string _groupName = string.Empty;

	private string _processID = string.Empty;

	private string _parentID = string.Empty;

	public string ProcessID
	{
		get
		{
			return _processID;
		}
		set
		{
			if (_processID != value)
			{
				_processID = value;
				OnPropertyChanged("ProcessID");
			}
		}
	}

	public string ParentID
	{
		get
		{
			return _parentID;
		}
		set
		{
			if (_parentID != value)
			{
				_parentID = value;
				OnPropertyChanged("ParentID");
			}
		}
	}

	public string Id
	{
		get
		{
			return _id;
		}
		set
		{
			if (_id != value)
			{
				_id = value;
				OnPropertyChanged("ID");
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
			if (_name != value)
			{
				_name = value;
				OnPropertyChanged("Name");
			}
		}
	}

	public string FullPath
	{
		get
		{
			return _fullPath;
		}
		set
		{
			if (_fullPath != value)
			{
				_fullPath = value;
				OnPropertyChanged("FullPath");
			}
		}
	}

	public string GroupName
	{
		get
		{
			return _groupName;
		}
		set
		{
			if (_groupName != value)
			{
				_groupName = value;
				OnPropertyChanged("GroupName");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public virtual void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}
}
