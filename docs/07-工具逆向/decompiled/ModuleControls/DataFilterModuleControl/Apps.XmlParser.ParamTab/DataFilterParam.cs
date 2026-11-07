using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Apps.Log;
using Mvvm.Commands;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class DataFilterParam : DataFilterBaseParam
{
	private const string _DefaultStartName = "out";

	public ObservableCollection<VmModule> _FrontModules;

	private ICommand _SelectedModuleChangedCommand = null;

	private DelegateCommand<object> _PreviewMouseWheelCommand = null;

	private bool hasRegistered;

	public override VmIOCollection TargetVmIOs => Owner?.Outputs;

	public override string DefaultStartName => "out";

	public ObservableCollection<VmModule> FrontModules
	{
		get
		{
			if (_FrontModules == null)
			{
				_FrontModules = new ObservableCollection<VmModule>();
			}
			return _FrontModules;
		}
		set
		{
			if (_FrontModules != value)
			{
				_FrontModules = value;
				OnPropertyChanged("FrontModules");
			}
		}
	}

	public ICommand SelectedModuleChangedCommand
	{
		get
		{
			if (_SelectedModuleChangedCommand == null)
			{
				_SelectedModuleChangedCommand = (ICommand)new DelegateCommand<object>((Action<object>)SelectedModuleChangedCmdExecute);
			}
			return _SelectedModuleChangedCommand;
		}
	}

	public ICommand PreviewMouseWheelCommand
	{
		get
		{
			if (_PreviewMouseWheelCommand == null)
			{
				_PreviewMouseWheelCommand = new DelegateCommand<object>((Action<object>)PreviewMouseWheelCommandExcute);
			}
			return (ICommand)_PreviewMouseWheelCommand;
		}
	}

	public override string CurrentParamName => "DynamicInputParam";

	public void SelectedModuleChangedCmdExecute(object data)
	{
		if (TargetVmIOs == null || data == null)
		{
			return;
		}
		VmModule vmModule = data as VmModule;
		List<VmIODecorate> list = new List<VmIODecorate>();
		foreach (object item in ItemsA)
		{
			if (item is VmIODecorate { DecorateSource: DynamicVmIO decorateSource } vmIODecorate && !decorateSource.SubscriptionInfo.Contains(vmModule.Name) && !string.IsNullOrEmpty(decorateSource.SubscriptionInfo))
			{
				list.Add(vmIODecorate);
			}
		}
		foreach (VmIODecorate item2 in list)
		{
			item2.DeleteIO();
		}
	}

	private void PreviewMouseWheelCommandExcute(object obj)
	{
		if (hasRegistered)
		{
		}
		Grid df;
		if ((df = obj as Grid) != null)
		{
			df.PreviewMouseWheel += (object sender, MouseWheelEventArgs e) =>
			{
				MouseWheelEventArgs e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
				{
					RoutedEvent = UIElement.MouseWheelEvent,
					Source = sender
				};
				df.RaiseEvent(e2);
			};
			hasRegistered = true;
		}
	}

	public override int GetParamUnit()
	{
		InitSelectedModule();
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return 1;
			}
			byte[] bt = ((IUserBytesData)vmModule.Params)["InputText"];
			byte[] bt2 = ((IUserBytesData)vmModule.Params)["GroupText"];
			byte[] bt3 = ((IUserBytesData)vmModule.Params)["ConditionText"];
			string value = StringUtil.UTF8GetString(bt);
			string text = StringUtil.UTF8GetString(bt2);
			string text2 = StringUtil.UTF8GetString(bt3);
			if (!(((vmModule.Params.Data as AlgorithmTabRoot).Tabs[1] as Tab).Collection[0] is DataFilterConfig dataFilterConfig))
			{
				return 1;
			}
			dataFilterConfig.ConditionGroups.Clear();
			if (!string.IsNullOrEmpty(text))
			{
				string[] array = text.Split(';');
				string[] array2 = array;
				foreach (string text3 in array2)
				{
					string[] array3 = text3.Split(':');
					if (array3.Count() == 2)
					{
						string conditionGroupName = array3[0].Replace("%", "");
						string value2 = array3[1];
						ConditionGroupItem conditionGroupItem = new ConditionGroupItem();
						conditionGroupItem.ConditionGroupID = Convert.ToInt32(value2);
						conditionGroupItem.ConditionGroupName = conditionGroupName;
						conditionGroupItem.SetParentNode(dataFilterConfig);
						conditionGroupItem.vmModule = vmModule;
						conditionGroupItem.owner = dataFilterConfig;
						dataFilterConfig.ConditionGroups.Add(conditionGroupItem);
					}
				}
			}
			if (!string.IsNullOrEmpty(text2))
			{
				string[] array4 = text2.Split(';');
				string[] array5 = array4;
				foreach (string text4 in array5)
				{
					string[] array6 = text4.Split(':');
					if (array6.Count() == 6)
					{
						string curValue = array6[0].Replace("%", "");
						string text5 = array6[1];
						string value3 = array6[2];
						string value4 = array6[3];
						int groupId = Convert.ToInt32(array6[4]);
						int conditionID = Convert.ToInt32(array6[5]);
						ConditionGroupItem conditionGroupItem2 = dataFilterConfig.ConditionGroups.FirstOrDefault((ConditionGroupItem c) => c.ConditionGroupID == groupId);
						ConditionItem conditionItem = new ConditionItem();
						conditionItem.ConditionGroupID = groupId;
						conditionItem.ConditionGroupName = conditionGroupItem2.ConditionGroupName;
						conditionItem.ConditionID = conditionID;
						conditionItem.owner = conditionGroupItem2;
						conditionItem.TypeName = text5;
						conditionItem.SetParentNode(conditionGroupItem2);
						if (conditionItem.FilterSelecter != null)
						{
							conditionItem.FilterSelecter.CurValue = curValue;
						}
						conditionItem.vmModule = vmModule;
						if (text5 == "int")
						{
							(conditionItem.IntegerBettween.Integers[0] as Integer).CurValue = Convert.ToInt32(value3);
							(conditionItem.IntegerBettween.Integers[1] as Integer).CurValue = Convert.ToInt32(value4);
						}
						else
						{
							(conditionItem.FilterBettween.Floats[0] as Float).CurValue = Convert.ToSingle(value3);
							(conditionItem.FilterBettween.Floats[1] as Float).CurValue = Convert.ToSingle(value4);
						}
						conditionGroupItem2.ConditionItems.Add(conditionItem);
					}
				}
				if (!string.IsNullOrEmpty(value))
				{
					string[] array7 = text2.Split(';');
				}
			}
			if (dataFilterConfig != null && dataFilterConfig != null && dataFilterConfig.ConditionGroups.Count > 0)
			{
				dataFilterConfig.SelectedConditionGroupItem = dataFilterConfig.ConditionGroups[0];
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
		return 0;
	}

	public void InitSelectedModule()
	{
		VmModule vmModule = GetVmModule();
		if (vmModule == null)
		{
			return;
		}
		List<VmModule> list = new List<VmModule>();
		if (vmModule.FrontModules.Count > 0)
		{
			foreach (object frontModule in vmModule.FrontModules)
			{
				if (frontModule is IMVSGroup)
				{
					list.Add(frontModule as VmModule);
					if (!FrontModules.Contains(frontModule))
					{
						FrontModules.Add(frontModule as VmModule);
					}
				}
				else if (!(frontModule is VmModuleSet))
				{
					list.Add(frontModule as VmModule);
					if (!FrontModules.Contains(frontModule))
					{
						FrontModules.Add(frontModule as VmModule);
					}
				}
			}
		}
		foreach (VmModule frontModule2 in FrontModules)
		{
			if (!list.Contains(frontModule2))
			{
				FrontModules.Remove(frontModule2);
			}
		}
		if (FrontModules.Count < 1)
		{
			return;
		}
		List<DynamicVmIO> list2 = (from p in vmModule.Outputs.OfType<DynamicVmIO>()
			where p.Visible
			select p).ToList();
		if (list2.Count >= 1)
		{
			foreach (VmModule frontModule3 in FrontModules)
			{
				if (list2[0].SubscriptionInfo.Contains(frontModule3.Name))
				{
					SelectedModule = frontModule3;
					return;
				}
			}
		}
		SelectedModule = FrontModules[0];
	}
}
