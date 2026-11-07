using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Mvvm.Commands;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class DataSortParam : DataSortBaseParam
{
	private const string _DefaultStartName = "out";

	public ObservableCollection<VmModule> _FrontModules;

	private ICommand _SelectedModuleChangedCommand = null;

	private DelegateCommand<object> _PreviewMouseWheelCommand = null;

	private bool hasRegistered;

	private bool isInit = false;

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
			byte[] bt = ((IUserBytesData)vmModule.Params)["SortText"];
			string text = StringUtil.UTF8GetString(bt);
			DataSortConfig dataSortConfig = ((vmModule.Params.Data as AlgorithmTabRoot).Tabs[1] as Tab).Collection[0] as DataSortConfig;
			dataSortConfig.SortItems.Clear();
			if (!string.IsNullOrEmpty(text))
			{
				string[] array = text.Split(';');
				string[] array2 = array;
				foreach (string text2 in array2)
				{
					string[] array3 = text2.Split(':');
					if (array3.Count() == 4)
					{
						string curValue = array3[0].Replace("%", "");
						int selectedSortTypeIndex = Convert.ToInt32(array3[1]);
						int sortID = Convert.ToInt32(array3[2]);
						string typeName = array3[3];
						SortItem sortItem = new SortItem();
						sortItem.SortID = sortID;
						sortItem.SelectedSortTypeIndex = selectedSortTypeIndex;
						sortItem.SortName = "排序" + sortID;
						sortItem.TypeName = typeName;
						sortItem.vmModule = vmModule;
						sortItem.owner = dataSortConfig;
						sortItem.SetParentNode(dataSortConfig);
						if (sortItem?.SortSelecter != null)
						{
							sortItem.SortSelecter.CurValue = curValue;
						}
						dataSortConfig.SortItems.Add(sortItem);
					}
				}
			}
			dataSortConfig.PointTypeSource = new ObservableCollection<string>
			{
				AppLocalizationService.GetLang("Clockwise"),
				AppLocalizationService.GetLang("Anticlockwise")
			};
			dataSortConfig.FloatTypeSource = new ObservableCollection<string>
			{
				AppLocalizationService.GetLang("Lang_顺序"),
				AppLocalizationService.GetLang("Lang_倒序")
			};
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
