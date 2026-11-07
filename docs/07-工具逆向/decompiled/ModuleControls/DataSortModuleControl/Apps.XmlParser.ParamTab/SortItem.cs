using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Hik.Xml;
using Mvvm.Commands;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class SortItem : NameValueItem
{
	public ButtonSelecter _SortSelecter;

	public string _TypeName;

	public int _SelectedSortTypeIndex;

	public int _SortID;

	public string _SortName;

	private ICommand _DeleteSortItemCommand = null;

	public VmModule vmModule { get; set; }

	public object owner { get; set; }

	public ButtonSelecter SortSelecter
	{
		get
		{
			if (_SortSelecter == null)
			{
				SortSelecterInit();
			}
			return _SortSelecter;
		}
	}

	public string TypeName
	{
		get
		{
			return _TypeName;
		}
		set
		{
			if (_TypeName != value)
			{
				_TypeName = value;
				OnPropertyChanged("TypeName");
			}
		}
	}

	public int SelectedSortTypeIndex
	{
		get
		{
			return _SelectedSortTypeIndex;
		}
		set
		{
			if (_SelectedSortTypeIndex != value)
			{
				_SelectedSortTypeIndex = value;
				OnPropertyChanged("SelectedSortTypeIndex");
				if (owner != null)
				{
					(owner as DataSortConfig).SetSortConfig();
				}
			}
		}
	}

	public int SortID
	{
		get
		{
			return _SortID;
		}
		set
		{
			if (_SortID != value)
			{
				_SortID = value;
				OnPropertyChanged("SortID");
			}
		}
	}

	public string SortName
	{
		get
		{
			return _SortName;
		}
		set
		{
			if (_SortName != value)
			{
				_SortName = value;
				OnPropertyChanged("SortName");
			}
		}
	}

	public ObservableCollection<string> RuleTypeSource
	{
		get
		{
			if (TypeName == "int" || TypeName == "float")
			{
				return (owner as DataSortConfig).FloatTypeSource;
			}
			return (owner as DataSortConfig).PointTypeSource;
		}
	}

	public ICommand DeleteSortItemCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_DeleteSortItemCommand == null)
			{
				_DeleteSortItemCommand = (ICommand)new DelegateCommand((Action)DeleteSortItemCommandExe);
			}
			return _DeleteSortItemCommand;
		}
	}

	public void SortSelecterInit()
	{
		BaseNode baseNode = GroupsRoot.GetInstance()["SubscriptionFilterSortByIO"];
		if (baseNode is GroupInfoItem)
		{
			GroupInfoItem groupInfoItem = (baseNode as GroupInfoItem).Clone<GroupInfoItem>();
			if (groupInfoItem != null && groupInfoItem.Items != null)
			{
				using IEnumerator<object> enumerator = groupInfoItem.Items.GetEnumerator();
				if (enumerator.MoveNext())
				{
					BaseNode baseNode2 = (BaseNode)enumerator.Current;
					_SortSelecter = baseNode2 as ButtonSelecter;
				}
			}
		}
		if (_SortSelecter != null)
		{
			_SortSelecter.SetParentNode(this);
			_SortSelecter.DropOpenSetter.OperationParams = TypeName;
			_SortSelecter.InniterBusiness();
			_SortSelecter.ReadOnlyA = true;
			_SortSelecter.OnCurValueChanged -= FilterSelecterParamValueChanged;
			_SortSelecter.OnCurValueChanged += FilterSelecterParamValueChanged;
		}
	}

	public void FilterSelecterParamValueChanged(object sender, EventArgs e)
	{
		(owner as DataSortConfig).SetSortConfig();
	}

	public void DeleteSortItemCommandExe()
	{
		if (owner is DataSortConfig dataSortConfig)
		{
			dataSortConfig.SortItems.Remove(this);
			dataSortConfig.SetSortConfig();
		}
	}
}
