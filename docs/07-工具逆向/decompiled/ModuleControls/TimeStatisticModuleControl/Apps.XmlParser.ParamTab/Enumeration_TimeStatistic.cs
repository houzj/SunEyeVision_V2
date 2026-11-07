using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using VM.Core;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class Enumeration_TimeStatistic : NameValueItem
{
	private const int startModule = 1;

	private const int endModule = 2;

	private const string arriveTimeLow = "fArrivalTimeStampLow";

	private const string arriveTimeHigh = "fArrivalTimeStampHigh";

	private const string leaveTimeLow = "fLeaveTimeStampLow";

	private const string leaveTimeHigh = "fLeaveTimeStampHigh";

	private List<VmModule> linkedModules;

	public ObservableCollection<object> EnumEntrys;

	public int CurValue;

	public int DefaultValue;

	public int Index;

	public int ModuleType;

	private DelegateCommand _SelectedChangedCommand = null;

	public override string DisplayType => "Enumeration";

	public ObservableCollection<object> EnumEntrysA
	{
		get
		{
			return EnumEntrys;
		}
		set
		{
			if (EnumEntrys != value)
			{
				EnumEntrys = value;
				OnPropertyChanged("EnumEntrysA");
			}
		}
	}

	public List<VmModule> LinkedModules
	{
		get
		{
			if (GetTopBaseNode() is AlgorithmTabRoot { Owner: not null })
			{
				VmModule vmModule = GetVmModule();
				if (vmModule != null)
				{
					linkedModules = vmModule.GetFrontParamItems();
				}
			}
			return linkedModules;
		}
	}

	public int DefaultValueA
	{
		get
		{
			return DefaultValue;
		}
		set
		{
			DefaultValue = value;
		}
	}

	public override string Value
	{
		get
		{
			if (CurValueA > 0)
			{
				return CurValueA.ToString();
			}
			return string.Empty;
		}
		set
		{
			CurValueA = int.Parse(value);
		}
	}

	public ICommand SelectedChangedCommand
	{
		get
		{
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand(SelectedChangedCmdExecute);
			}
			return _SelectedChangedCommand;
		}
	}

	public int IndexA
	{
		get
		{
			return Index;
		}
		set
		{
			if (Index != value)
			{
				Index = value;
			}
		}
	}

	public virtual int CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (CurValue == value)
			{
				return;
			}
			int curValue = CurValue;
			CurValue = value;
			try
			{
				if (CurValue > 0)
				{
					SetFilterSource(CurValue - 1);
				}
				else
				{
					ClearSubscription();
				}
				GetIndex();
				GetVmModule()?.ReFlashReadyState();
				OnPropertyChanged("CurValueA");
			}
			catch
			{
			}
		}
	}

	public void SelectedChangedCmdExecute()
	{
		OldValue = Value;
		if (GetParentNode() is TsDynamicIO tsDynamicIO)
		{
			tsDynamicIO.SetParamUnit();
		}
	}

	public void SetFilterSource(int linkedNodeID)
	{
		try
		{
			if (!(GetTopBaseNode() is AlgorithmTabRoot { Owner: not null }))
			{
				return;
			}
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			List<VmModule> list = LinkedModules;
			if (list == null)
			{
				return;
			}
			VmModule vmModule2 = list.Find((VmModule p) => p.ID == linkedNodeID);
			if (vmModule2 == null)
			{
				return;
			}
			string text = "";
			string text2 = "";
			if (ModuleType == 1)
			{
				VmIO vmIO = vmModule2.Outputs.FirstOrDefault((VmIO p) => p.Name == "fArrivalTimeStampLow");
				VmIO vmIO2 = vmModule2.Outputs.FirstOrDefault((VmIO p) => p.Name == "fArrivalTimeStampHigh");
				if (vmIO != null && vmIO2 != null)
				{
					text = vmIO.FullName;
					text2 = vmIO2.FullName;
					VmIO inputFilter = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fArrivalTimeStampLow");
					VmIO inputFilter2 = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fArrivalTimeStampHigh");
					SetFilterSourceOperation.SetFilterSource(vmModule, inputFilter, text);
					SetFilterSourceOperation.SetFilterSource(vmModule, inputFilter2, text2);
				}
			}
			else
			{
				if (ModuleType != 2)
				{
					return;
				}
				VmIO vmIO3 = vmModule2.Outputs.FirstOrDefault((VmIO p) => p.Name == "fLeaveTimeStampLow");
				VmIO vmIO4 = vmModule2.Outputs.FirstOrDefault((VmIO p) => p.Name == "fLeaveTimeStampHigh");
				if (vmIO3 != null && vmIO4 != null)
				{
					text = vmIO3.FullName;
					text2 = vmIO4.FullName;
					VmIO inputFilter3 = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fLeaveTimeStampLow");
					VmIO inputFilter4 = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fLeaveTimeStampHigh");
					SetFilterSourceOperation.SetFilterSource(vmModule, inputFilter3, text);
					SetFilterSourceOperation.SetFilterSource(vmModule, inputFilter4, text2);
				}
			}
		}
		catch
		{
		}
	}

	public void GetIndex()
	{
		if (EnumEntrys == null)
		{
			return;
		}
		for (int i = 0; i < EnumEntrys.Count; i++)
		{
			if ((EnumEntrys[i] as EnumEntry).Value == CurValueA)
			{
				Index = i;
			}
		}
	}

	public int GetSelectParam()
	{
		return 0;
	}

	public bool IsSubscriptionExist()
	{
		if (GetTopBaseNode() is AlgorithmTabRoot { Owner: not null })
		{
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return false;
			}
			if (ModuleType == 1)
			{
				VmIO vmIO = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fArrivalTimeStampLow");
				VmIO vmIO2 = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fArrivalTimeStampHigh");
				if (vmIO == null || vmIO2 == null)
				{
					return false;
				}
				if (vmIO.RelateIO != null && vmIO2.RelateIO != null)
				{
					return true;
				}
				return false;
			}
			if (ModuleType == 2)
			{
				VmIO vmIO3 = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fLeaveTimeStampLow");
				VmIO vmIO4 = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fLeaveTimeStampHigh");
				if (vmIO3 == null || vmIO4 == null)
				{
					return false;
				}
				if (vmIO3.RelateIO != null && vmIO4.RelateIO != null)
				{
					return true;
				}
				return false;
			}
		}
		return false;
	}

	public void ClearSubscription()
	{
		try
		{
			if (!(GetTopBaseNode() is AlgorithmTabRoot { Owner: not null }))
			{
				return;
			}
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			if (ModuleType == 1)
			{
				VmIO vmIO = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fArrivalTimeStampLow");
				VmIO vmIO2 = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fArrivalTimeStampHigh");
				vmIO.RelateIO = null;
				vmIO2.RelateIO = null;
			}
			else if (ModuleType == 2)
			{
				VmIO vmIO3 = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fLeaveTimeStampLow");
				VmIO vmIO4 = vmModule.Inputs.FirstOrDefault((VmIO p) => p.Name == "fLeaveTimeStampHigh");
				vmIO3.RelateIO = null;
				vmIO4.RelateIO = null;
			}
			VmProcedure root = vmModule.Root;
			if (root != null)
			{
			}
		}
		catch
		{
		}
	}
}
