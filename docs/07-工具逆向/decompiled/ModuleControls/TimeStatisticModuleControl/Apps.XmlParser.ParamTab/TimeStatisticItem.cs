using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Log;
using Mvvm.Commands;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class TimeStatisticItem : NameValueItem
{
	public TimeConfig timeConfig;

	public int _TimeSeq;

	public string _TimeName;

	public VmModule _StartModule;

	public VmModule _EndModule;

	public ObservableCollection<VmModule> _AllModules;

	public ObservableCollection<VmModule> _StartModules;

	public ObservableCollection<VmModule> _EndModules;

	private ICommand _DeleteCommond = null;

	public int TimeSeq
	{
		get
		{
			return _TimeSeq;
		}
		set
		{
			if (_TimeSeq != value)
			{
				_TimeSeq = value;
				OnPropertyChanged("TimeSeq");
			}
		}
	}

	public string TimeName
	{
		get
		{
			return _TimeName;
		}
		set
		{
			if (_TimeName != value)
			{
				_TimeName = value;
				OnPropertyChanged("TimeName");
			}
		}
	}

	public VmModule StartModule
	{
		get
		{
			return _StartModule;
		}
		set
		{
			if (_StartModule == value)
			{
				return;
			}
			_StartModule = value;
			if (!timeConfig.isInit && value != null)
			{
				VmModule vmModule = timeConfig.GetVmModule();
				if (vmModule == null)
				{
					return;
				}
				EndModules = new ObservableCollection<VmModule>(vmModule.GetFrontToCurParamItems(value));
				VmModule endModule = _EndModule;
				if (_EndModules.Contains(endModule))
				{
					EndModule = endModule;
				}
				timeConfig.SetTimeParam();
			}
			OnPropertyChanged("StartModule");
		}
	}

	public VmModule EndModule
	{
		get
		{
			return _EndModule;
		}
		set
		{
			if (_EndModule != value)
			{
				_EndModule = value;
				if (!timeConfig.isInit && value != null)
				{
					timeConfig.SetTimeParam();
				}
				OnPropertyChanged("EndModule");
			}
		}
	}

	public ObservableCollection<VmModule> AllModules
	{
		get
		{
			if (_AllModules == null)
			{
				_AllModules = new ObservableCollection<VmModule>();
			}
			return _AllModules;
		}
		set
		{
			if (_AllModules != value)
			{
				_AllModules = value;
				OnPropertyChanged("AllModules");
			}
		}
	}

	public ObservableCollection<VmModule> StartModules
	{
		get
		{
			if (_StartModules == null)
			{
				_StartModules = new ObservableCollection<VmModule>();
			}
			return _StartModules;
		}
		set
		{
			if (_StartModules != value)
			{
				_StartModules = value;
				OnPropertyChanged("StartModules");
			}
		}
	}

	public ObservableCollection<VmModule> EndModules
	{
		get
		{
			if (_EndModules == null)
			{
				_EndModules = new ObservableCollection<VmModule>();
			}
			return _EndModules;
		}
		set
		{
			if (_EndModules != value)
			{
				_EndModules = value;
				OnPropertyChanged("EndModules");
			}
		}
	}

	public ICommand DeleteCommond
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_DeleteCommond == null)
			{
				_DeleteCommond = (ICommand)new DelegateCommand((Action)DeleteCmdExecute);
			}
			return _DeleteCommond;
		}
	}

	public void DeleteCmdExecute()
	{
		DeletaIO();
		timeConfig.TimeStatisticItems.Remove(this);
		timeConfig.SetTimeParam();
	}

	public void DeletaIO()
	{
		try
		{
			(timeConfig?.GetVmModule())?.Outputs.FirstOrDefault((VmIO c) => c.Name.Replace("%", "") == TimeName)?.RemoveSelf();
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}
}
