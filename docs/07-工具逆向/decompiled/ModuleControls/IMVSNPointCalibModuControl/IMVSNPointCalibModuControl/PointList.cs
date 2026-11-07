using System.Collections;
using System.ComponentModel;
using System.Windows.Input;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using VM.Utility;
using VMControls.BaseInterface;

namespace IMVSNPointCalibModuControl;

public class PointList : INotifyPropertyChanged
{
	public string _ID;

	public string _ImagePointX = "0";

	public string _ImagePointY = "0";

	public string _WorldPointX = "0";

	public string _WorldPointY = "0";

	public string _DeltaAngle = "0";

	private DelegateCommand _DeleteCommand = null;

	public IParamsConfig OwnerParam { get; set; }

	public AlgorithmTabRoot TabRoot { get; set; }

	public IList OwnerList { get; set; }

	public string ID
	{
		get
		{
			return _ID;
		}
		set
		{
			if (_ID != value)
			{
				_ID = value;
				OnPropertyChanged("ID");
			}
		}
	}

	public string ImagePointX
	{
		get
		{
			return _ImagePointX;
		}
		set
		{
			if (!(_ImagePointX != value))
			{
				return;
			}
			string key = "ImagePointX" + ID;
			if (OwnerParam != null)
			{
				try
				{
					((IUserStringData)OwnerParam)[key] = value;
				}
				catch
				{
					return;
				}
				_ImagePointX = value;
				OnPropertyChanged("ImagePointX");
			}
		}
	}

	public string ImagePointY
	{
		get
		{
			return _ImagePointY;
		}
		set
		{
			if (!(_ImagePointY != value))
			{
				return;
			}
			string key = "ImagePointY" + ID;
			if (OwnerParam != null)
			{
				try
				{
					((IUserStringData)OwnerParam)[key] = value;
				}
				catch
				{
					return;
				}
				_ImagePointY = value;
				OnPropertyChanged("ImagePointY");
			}
		}
	}

	public string WorldPointX
	{
		get
		{
			return _WorldPointX;
		}
		set
		{
			if (!(_WorldPointX != value))
			{
				return;
			}
			string key = "WorldPointX" + ID;
			if (OwnerParam != null)
			{
				try
				{
					((IUserStringData)OwnerParam)[key] = value;
				}
				catch
				{
					return;
				}
				_WorldPointX = value;
				OnPropertyChanged("WorldPointX");
			}
		}
	}

	public string WorldPointY
	{
		get
		{
			return _WorldPointY;
		}
		set
		{
			if (!(_WorldPointY != value))
			{
				return;
			}
			string key = "WorldPointY" + ID;
			if (OwnerParam != null)
			{
				try
				{
					((IUserStringData)OwnerParam)[key] = value;
				}
				catch
				{
					return;
				}
				_WorldPointY = value;
				OnPropertyChanged("WorldPointY");
			}
		}
	}

	public string DeltaAngle
	{
		get
		{
			return _DeltaAngle;
		}
		set
		{
			if (!(_DeltaAngle != value))
			{
				return;
			}
			string key = "DeltaAngle" + ID;
			if (OwnerParam != null)
			{
				try
				{
					((IUserStringData)OwnerParam)[key] = value;
				}
				catch
				{
					return;
				}
				_DeltaAngle = value;
				OnPropertyChanged("DeltaAngle");
			}
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			if (_DeleteCommand == null)
			{
				_DeleteCommand = new DelegateCommand(DeleteCommandCmdExecute);
			}
			return _DeleteCommand;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public PointList(IParamsConfig ownerParam)
	{
		OwnerParam = ownerParam;
	}

	private void DeleteCommandCmdExecute()
	{
		if (!OwnerList.Contains(this))
		{
			return;
		}
		OwnerList.Remove(this);
		if (OwnerParam == null)
		{
			return;
		}
		try
		{
			((IUserStringData)OwnerParam)["DeletePointByIndex"] = ID;
		}
		catch
		{
			return;
		}
		if (TabRoot != null)
		{
			BaseNode baseNode = TabRoot.FindBaseNodeByName("CalibPointTotalNum");
			if (baseNode is NameValueItem)
			{
				(baseNode as NameValueItem).GetParamUnit();
			}
			BaseNode baseNode2 = TabRoot.FindBaseNodeByName("RotPointTotalNum");
			if (baseNode2 is NameValueItem)
			{
				(baseNode2 as NameValueItem).GetParamUnit();
			}
		}
		foreach (object owner in OwnerList)
		{
			(owner as PointList).ID = OwnerList.IndexOf(owner).ToString();
		}
	}

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}
}
