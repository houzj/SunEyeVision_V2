using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class AddList : NameValueItem
{
	public ObservableCollection<HeadItem> Headers;

	private ObservableCollection<SubData> EnumEntrys = new ObservableCollection<SubData>();

	private DelegateCommand _ClickCommand = null;

	public ObservableCollection<HeadItem> HeadersA
	{
		get
		{
			return Headers;
		}
		set
		{
			if (Headers != value)
			{
				Headers = value;
				OnPropertyChanged("HeadersA");
			}
		}
	}

	public ObservableCollection<SubData> EnumEntrysA
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

	public ICommand ClickCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand((Action)ClickCommandCmdExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
	}

	private void ClickCommandCmdExecute()
	{
		DoAdd();
	}

	public virtual void DoAdd(string value = null, string hashCode = null, int moduleId = -1)
	{
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}

	protected void SetEnumEntrysNull()
	{
		EnumEntrys = new ObservableCollection<SubData>();
	}
}
