using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Mvvm.Commands;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class EnumerationTrigger : Enumeration
{
	private int _triggerProcessId = -1;

	private IList _EnumEntrys = null;

	private DelegateCommand _SelectedChangedCommand = null;

	public override string DisplayType => "EnumerationTrigger";

	public int TriggerProcessID
	{
		get
		{
			return _triggerProcessId;
		}
		set
		{
			if (_triggerProcessId != value)
			{
				_triggerProcessId = value;
				OnPropertyChanged("TriggerProcessID");
			}
		}
	}

	public new virtual IList EnumEntrysA
	{
		get
		{
			_EnumEntrys = GetAllProcess();
			return _EnumEntrys;
		}
	}

	public new ICommand SelectedChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand((Action)SelectedChangedCmdExecute);
			}
			return (ICommand)_SelectedChangedCommand;
		}
	}

	public ObservableCollection<EnumEntry> GetAllProcess()
	{
		ObservableCollection<EnumEntry> observableCollection = new ObservableCollection<EnumEntry>();
		object obj = GetVmModule()?.Parent;
		while (!(obj is VmProcedure))
		{
			obj = (obj as VmModuleSet).Parent;
		}
		foreach (VmProcedure item in VmSolution.Instance.Modules.OfType<VmProcedure>())
		{
			if (item.ID != (obj as VmProcedure).ID)
			{
				observableCollection.Add(new EnumEntry
				{
					DisplayNameA = item.FullName,
					Value = (int)item.ID
				});
			}
		}
		return observableCollection;
	}

	public override int GetParamUnit()
	{
		TriggerProcessID = int.Parse(GetVmModule().Params["TriggerProcedureID"]);
		return 0;
	}

	private void SelectedChangedCmdExecute()
	{
		if (!IsUnConnect())
		{
			GetVmModule().Params["TriggerProcedureID"] = TriggerProcessID.ToString();
		}
	}
}
