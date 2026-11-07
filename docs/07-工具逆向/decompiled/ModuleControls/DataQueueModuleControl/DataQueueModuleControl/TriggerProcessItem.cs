using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Apps.XmlParser.ParamTab;
using VM.Core;
using VMControls.Interface;

namespace DataQueueModuleControl;

public class TriggerProcessItem : INotifyPropertyChanged
{
	private int index;

	private int _triggerProcessId = -1;

	public IList ProcessList => GetAllProcess();

	public int Index
	{
		get
		{
			return index;
		}
		set
		{
			if (index != value)
			{
				index = value;
				OnPropertyChanged("Index");
			}
		}
	}

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

	public string TriggerProcessData => Index + "#" + TriggerProcessID;

	public event PropertyChangedEventHandler PropertyChanged;

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	private ObservableCollection<EnumEntry> GetAllProcess()
	{
		ObservableCollection<EnumEntry> observableCollection = new ObservableCollection<EnumEntry>();
		foreach (VmProcedure item in ((IVmSolution)VmSolution.Instance).GetAllModule().OfType<VmProcedure>())
		{
			observableCollection.Add(new EnumEntry
			{
				DisplayNameA = item.FullName,
				Value = (int)item.ID
			});
		}
		return observableCollection;
	}
}
