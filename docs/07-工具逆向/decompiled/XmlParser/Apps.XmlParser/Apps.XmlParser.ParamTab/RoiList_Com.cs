using System;
using System.ComponentModel;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class RoiList_Com : INotifyPropertyChanged
{
	public delegate void EventHandler(RoiType roi);

	public delegate void SelectHandler(int selectnum);

	public int label;

	public bool IsShowIndex = false;

	protected ROIType roitypecom;

	protected bool LineCalipercom = false;

	protected bool CircleCalipercom;

	private bool LineCaliper_ModuleEnable;

	private bool CircleCaliper_ModuleEnable;

	private DelegateCommand<object> _ValueChangedCommand = null;

	public int Label
	{
		get
		{
			return label;
		}
		set
		{
			if (label != value)
			{
				label = value;
				OnPropertyChanged("Num");
			}
		}
	}

	public bool IsShowIndexA
	{
		get
		{
			return IsShowIndex;
		}
		set
		{
			if (IsShowIndex != value)
			{
				IsShowIndex = value;
				OnPropertyChanged("IsShowIndexA");
			}
		}
	}

	public ROIType RoiTypeCom
	{
		get
		{
			return roitypecom;
		}
		set
		{
			if (roitypecom != value)
			{
				roitypecom = value;
				OnPropertyChanged("RoiTypeCom");
			}
		}
	}

	public bool LineCaliperComA
	{
		get
		{
			return LineCalipercom;
		}
		set
		{
			if (LineCalipercom != value)
			{
				LineCalipercom = value;
				OnPropertyChanged("LineCaliperComA");
			}
		}
	}

	public bool CircleCaliperComA
	{
		get
		{
			return CircleCalipercom;
		}
		set
		{
			if (CircleCalipercom != value)
			{
				CircleCalipercom = value;
				OnPropertyChanged("CircleCaliperComA");
			}
		}
	}

	public virtual bool LineCaliper_ModuleEnableA
	{
		get
		{
			return LineCaliper_ModuleEnable;
		}
		set
		{
			if (LineCaliper_ModuleEnable != value)
			{
				LineCaliper_ModuleEnable = value;
				OnPropertyChanged("LineCaliper_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public virtual bool CircleCaliper_ModuleEnableA
	{
		get
		{
			return CircleCaliper_ModuleEnable;
		}
		set
		{
			if (CircleCaliper_ModuleEnable != value)
			{
				CircleCaliper_ModuleEnable = value;
				OnPropertyChanged("CircleCaliper_ModuleEnableA");
				OnPropertyChanged("IsHaveData");
			}
		}
	}

	public bool IsHaveData => LineCaliper_ModuleEnable || CircleCaliper_ModuleEnable;

	public ICommand ValueChangedCommand
	{
		get
		{
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand<object>((Action<object>)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event EventHandler NoticeDrawEvent;

	public event SelectHandler NoticeSelectnumEvent;

	public virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	protected void ValueChangedCmdExecute(object type)
	{
		if (type is RoiType roiType)
		{
			DoRadioButton(ref roiType);
			NoticeDrawEvent(roiType);
			NoticeSelectnumEvent(Label);
		}
	}

	protected void DoRadioButton(ref RoiType roiType)
	{
		switch (roiType)
		{
		case RoiType.LineCaliper:
			if (LineCaliperComA)
			{
				SelectNone();
				LineCaliperComA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		case RoiType.CircleCaliper:
			if (CircleCaliperComA)
			{
				SelectNone();
				CircleCaliperComA = true;
			}
			else
			{
				roiType = RoiType.Default;
			}
			break;
		default:
			SelectNone();
			roiType = RoiType.Default;
			break;
		}
	}

	protected void SelectNone()
	{
		LineCaliperComA = false;
		CircleCaliperComA = false;
	}
}
