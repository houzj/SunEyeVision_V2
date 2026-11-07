using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class DataFilterConfig : NameValueItem
{
	public ObservableCollection<ConditionGroupItem> _ConditionGroups;

	private ICommand _AddConditionGroupCommand = null;

	public ConditionGroupItem _SelectedConditionGroupItem;

	public ObservableCollection<ConditionGroupItem> ConditionGroups
	{
		get
		{
			if (_ConditionGroups == null)
			{
				_ConditionGroups = new ObservableCollection<ConditionGroupItem>();
			}
			return _ConditionGroups;
		}
		set
		{
			if (_ConditionGroups != value)
			{
				_ConditionGroups = value;
				OnPropertyChanged("ConditionGroups");
			}
		}
	}

	public ICommand AddConditionGroupCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_AddConditionGroupCommand == null)
			{
				_AddConditionGroupCommand = (ICommand)new DelegateCommand((Action)AddConditionGroupCmdExecute);
			}
			return _AddConditionGroupCommand;
		}
	}

	public ConditionGroupItem SelectedConditionGroupItem
	{
		get
		{
			return _SelectedConditionGroupItem;
		}
		set
		{
			if (_SelectedConditionGroupItem != value)
			{
				_SelectedConditionGroupItem = value;
				OnPropertyChanged("SelectedConditionGroupItem");
			}
		}
	}

	public void AddConditionGroupCmdExecute()
	{
		if (Enumerable.Count(ConditionGroups) >= 10)
		{
			string strMsg = AppLocalizationService.GetLang("Lang_ConditionGroupCountLimitError") + $"【{10}】";
			HikMessageBox.Show(strMsg);
			return;
		}
		VmModule vmModule = (GetTopBaseNode() as AlgorithmTabRoot).Owner as VmModule;
		int i;
		for (i = 0; !CheckGroupName(i); i++)
		{
			if (i >= 10)
			{
				return;
			}
		}
		ConditionGroupItem conditionGroupItem = new ConditionGroupItem();
		conditionGroupItem.ConditionGroupID = i;
		conditionGroupItem.ConditionGroupName = AppLocalizationService.GetLang("RunParam_Conditioned") + i;
		conditionGroupItem.vmModule = vmModule;
		conditionGroupItem.owner = this;
		conditionGroupItem.SetParentNode(this);
		ConditionGroups.Add(conditionGroupItem);
		SetParamUnitGroup();
		SelectedConditionGroupItem = conditionGroupItem;
	}

	public bool CheckGroupName(int seq)
	{
		foreach (ConditionGroupItem conditionGroup in ConditionGroups)
		{
			if (conditionGroup.ConditionGroupID == seq)
			{
				return false;
			}
		}
		return true;
	}

	public void SetParamUnitInput()
	{
		try
		{
			string text = "";
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			DataFilterParam dataFilterParam = ((vmModule.Params.Data as AlgorithmTabRoot).Tabs[0] as Tab).Collection[0] as DataFilterParam;
			foreach (object item in dataFilterParam.ItemsA)
			{
				VmIODecorate vmIODecorate = item as VmIODecorate;
				string text2 = $"{vmIODecorate.Name}:{vmIODecorate.ValueType}:{vmIODecorate.FilterSelecter?.CurValue};";
				text += text2;
			}
			((IUserBytesData)vmModule.Params)["InputText"] = StringUtil.UTF8GetBytesPadZero(text);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public void SetParamUnitGroup()
	{
		try
		{
			string text = "";
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			foreach (ConditionGroupItem conditionGroup in ConditionGroups)
			{
				string text2 = $"%{conditionGroup.ConditionGroupName}%:{conditionGroup.ConditionGroupID};";
				text += text2;
			}
			((IUserBytesData)vmModule.Params)["GroupText"] = StringUtil.UTF8GetBytesPadZero(text);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public void SetParamUnitCondition()
	{
		try
		{
			string text = "";
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			foreach (ConditionGroupItem conditionGroup in ConditionGroups)
			{
				foreach (ConditionItem conditionItem in conditionGroup.ConditionItems)
				{
					string text2 = "0";
					string text3 = "0";
					if (conditionItem.TypeName == "int")
					{
						Integer integer = conditionItem.IntegerBettween.IntegersA[0] as Integer;
						Integer integer2 = conditionItem.IntegerBettween.IntegersA[1] as Integer;
						text2 = (integer.IsSubA ? integer.SubData.CurValue : integer.CurValue.ToString());
						text3 = (integer2.IsSubA ? integer2.SubData.CurValue : integer2.CurValue.ToString());
					}
					else
					{
						Float obj = conditionItem.FilterBettween.Floats[0] as Float;
						Float obj2 = conditionItem.FilterBettween.Floats[1] as Float;
						text2 = (obj.IsSubA ? obj.SubData.CurValue : obj.CurValue.ToString());
						text3 = (obj2.IsSubA ? obj2.SubData.CurValue : obj2.CurValue.ToString());
					}
					if (conditionItem?.FilterSelecter != null)
					{
						string text4 = $"%{conditionItem?.FilterSelecter?.Value}%:{conditionItem.TypeName}:{text2}:{text3}:{conditionItem.ConditionGroupID}:{conditionItem.ConditionID};";
						text += text4;
					}
				}
			}
			((IUserBytesData)vmModule.Params)["ConditionText"] = StringUtil.UTF8GetBytesPadZero(text);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}
}
