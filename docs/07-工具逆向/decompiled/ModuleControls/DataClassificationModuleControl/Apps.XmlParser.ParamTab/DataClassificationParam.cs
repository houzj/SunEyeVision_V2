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

public class DataClassificationParam : DataClassificationBaseParam
{
	private const string _DefaultStartName = "in";

	public ObservableCollection<VmModule> _FrontModules;

	private ICommand _SelectedModuleChangedCommand = null;

	private DelegateCommand<object> _PreviewMouseWheelCommand = null;

	private bool hasRegistered;

	public override VmIOCollection TargetVmIOs => Owner?.Inputs;

	public override string DefaultStartName => "in";

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
			byte[] bt2 = ((IUserBytesData)vmModule.Params)["IsIncompatible"];
			byte[] bt3 = ((IUserBytesData)vmModule.Params)["ClassText"];
			byte[] bt4 = ((IUserBytesData)vmModule.Params)["GroupText"];
			byte[] bt5 = ((IUserBytesData)vmModule.Params)["ConditionText"];
			string value = StringUtil.UTF8GetString(bt);
			string text = StringUtil.UTF8GetString(bt2);
			string text2 = StringUtil.UTF8GetString(bt3);
			string text3 = StringUtil.UTF8GetString(bt4);
			string text4 = StringUtil.UTF8GetString(bt5);
			DataClassificationConfig config = ((vmModule.Params.Data as AlgorithmTabRoot).Tabs[1] as Tab).Collection[0] as DataClassificationConfig;
			config.ClassificationItems.Clear();
			if (!string.IsNullOrEmpty(text))
			{
				config.BooleanData.CurValue = text == "1";
			}
			if (!string.IsNullOrEmpty(text2))
			{
				List<ClassificationItem> list = new List<ClassificationItem>();
				string[] array = text2.Split(';');
				string[] array2 = array;
				foreach (string text5 in array2)
				{
					string[] array3 = text5.Split(':');
					if (array3.Count() == 2)
					{
						string classificationName = array3[0];
						int num = Convert.ToInt32(array3[1]);
						ClassificationItem classificationItem = new ClassificationItem();
						classificationItem.ClassificationName = classificationName;
						classificationItem.vmModule = vmModule;
						classificationItem.config = config;
						classificationItem.SetParentNode(config);
						list.Add(classificationItem);
					}
				}
				list.ToList().ForEach((ClassificationItem c) =>
				{
					config.ClassificationItems.Add(c);
				});
				config.NotifySeq();
			}
			if (!string.IsNullOrEmpty(text3))
			{
				string[] array4 = text3.Split(';');
				string[] array5 = array4;
				foreach (string text6 in array5)
				{
					string[] array6 = text6.Split(':');
					if (array6.Count() == 3)
					{
						int value2 = Convert.ToInt32(array6[1]);
						string conditionGroupName = array6[0].ToString();
						string className = array6[2].ToString();
						ConditionGroupItem conditionGroupItem = new ConditionGroupItem();
						conditionGroupItem.ConditionGroupID = Convert.ToInt32(value2);
						conditionGroupItem.ConditionGroupName = conditionGroupName;
						conditionGroupItem.vmModule = vmModule;
						conditionGroupItem.config = config;
						ClassificationItem classificationItem2 = (conditionGroupItem.owner = config.ClassificationItems.FirstOrDefault((ClassificationItem c) => c.ClassificationName == className));
						conditionGroupItem.SetParentNode(classificationItem2);
						classificationItem2.ConditionGroupItems.Add(conditionGroupItem);
					}
				}
			}
			if (!string.IsNullOrEmpty(text4))
			{
				string[] array7 = text4.Split(';');
				string[] array8 = array7;
				foreach (string text7 in array8)
				{
					string[] array9 = text7.Split(':');
					if (array9.Count() == 7)
					{
						string curValue = array9[0].Replace("%", "");
						string text8 = array9[1];
						string value3 = array9[2];
						string value4 = array9[3];
						int groupID = Convert.ToInt32(array9[4]);
						int conditionID = Convert.ToInt32(array9[5]);
						string className2 = array9[6];
						ClassificationItem classificationItem4 = config.ClassificationItems.FirstOrDefault((ClassificationItem c) => c.ClassificationName == className2);
						ConditionGroupItem conditionGroupItem2 = classificationItem4.ConditionGroupItems.FirstOrDefault((ConditionGroupItem c) => c.ConditionGroupID == groupID);
						ConditionItem conditionItem = new ConditionItem();
						conditionItem.ConditionGroupID = groupID;
						conditionItem.ConditionGroupName = conditionGroupItem2.ConditionGroupName;
						conditionItem.ConditionID = conditionID;
						conditionItem.owner = conditionGroupItem2;
						conditionItem.config = config;
						conditionItem.vmModule = vmModule;
						conditionItem.classItem = classificationItem4;
						conditionItem.TypeName = text8;
						conditionItem.SetParentNode(conditionGroupItem2);
						if (conditionItem?.ClassificationSelecter != null)
						{
							conditionItem.ClassificationSelecter.CurValue = curValue;
						}
						if (text8 == "int")
						{
							(conditionItem.IntegerBettween.Integers[0] as Integer).CurValue = Convert.ToInt32(value3);
							(conditionItem.IntegerBettween.Integers[1] as Integer).CurValue = Convert.ToInt32(value4);
						}
						else
						{
							(conditionItem.ClassificationBettween.Floats[0] as Float).CurValue = Convert.ToDouble(value3);
							(conditionItem.ClassificationBettween.Floats[1] as Float).CurValue = Convert.ToDouble(value4);
						}
						conditionGroupItem2.ConditionItems.Add(conditionItem);
					}
				}
				if (!string.IsNullOrEmpty(value))
				{
					string[] array10 = text4.Split(';');
				}
			}
			if (config != null)
			{
				DataClassificationConfig dataClassificationConfig = config;
				if (dataClassificationConfig != null && dataClassificationConfig.ClassificationItems.Count > 0)
				{
					config.SelectedClassificationItem = config.ClassificationItems[0];
				}
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
		if (vmModule.FrontModules.Count > 0)
		{
			FrontModules.Clear();
			foreach (object frontModule in vmModule.FrontModules)
			{
				if (frontModule is IMVSGroup)
				{
					FrontModules.Add(frontModule as VmModule);
				}
				else if (!(frontModule is VmModuleSet))
				{
					FrontModules.Add(frontModule as VmModule);
				}
			}
		}
		if (FrontModules.Count < 1)
		{
			return;
		}
		List<DynamicVmIO> list = (from p in vmModule.Inputs.OfType<DynamicVmIO>()
			where p.Visible
			select p).ToList();
		if (list.Count >= 1)
		{
			foreach (VmModule frontModule2 in FrontModules)
			{
				if (list[0].SubscriptionInfo.Contains(frontModule2.Name))
				{
					SelectedModule = frontModule2;
					return;
				}
			}
		}
		SelectedModule = FrontModules[0];
	}
}
