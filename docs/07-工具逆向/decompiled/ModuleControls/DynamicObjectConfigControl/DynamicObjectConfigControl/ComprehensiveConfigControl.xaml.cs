using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using DynamicObjectConfigControl.XmlData;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;
using VMControls.WPF;
using VMControls.WPF.DataForUI;

namespace DynamicObjectConfigControl;

public partial class ComprehensiveConfigControl : UserControl, INotifyPropertyChanged, ISpecialModuleParamTabVisible, IComponentConnector
{
	private const string GlobalParamNameKey = "GlobalParamNameKey";

	private const string GlobalParamIdKey = "GlobalParamIdKey";

	private ObservableCollection<GlobalParamConfigItem> _GlobalParamItems;

	private GlobalParamConfigItem _SelectedGlobalItem;

	private GlobalParamConfigData _GlobalData;

	private bool _IsMultiRun;

	private IDecorate _ImageDecorate;

	private Window _parentWindow;

	private bool _isWindowClosing;

	private DelegateCommand _EditCommand = null;

	private DelegateCommand<string> _SelectCommand = null;

	public ObservableCollection<GlobalParamConfigItem> GlobalParamItems
	{
		get
		{
			if (_GlobalParamItems == null)
			{
				_GlobalParamItems = new ObservableCollection<GlobalParamConfigItem>();
			}
			return _GlobalParamItems;
		}
		set
		{
			if (_GlobalParamItems != value)
			{
				_GlobalParamItems = value;
				OnPropertyChanged("GlobalParamItems");
			}
		}
	}

	public GlobalParamConfigItem SelectedGlobalItem
	{
		get
		{
			return _SelectedGlobalItem;
		}
		set
		{
			if (_SelectedGlobalItem != value)
			{
				_SelectedGlobalItem = value;
				OnPropertyChanged("SelectedGlobalItem");
			}
		}
	}

	public bool IsEmpty => GlobalParamItems == null || GlobalParamItems.Count == 0;

	public List<string> GlobalOptions => GlobalParamItems?.Select((GlobalParamConfigItem p) => p.Name).ToList();

	public GlobalParamConfigData GlobalData
	{
		get
		{
			if (_GlobalData == null)
			{
				_GlobalData = new GlobalParamConfigData();
			}
			return _GlobalData;
		}
	}

	public bool IsMultiRun
	{
		get
		{
			return _IsMultiRun;
		}
		set
		{
			if (_IsMultiRun != value)
			{
				_IsMultiRun = value;
				OnPropertyChanged("IsMultiRun");
			}
		}
	}

	public IDecorate ImageDecorate
	{
		get
		{
			return _ImageDecorate;
		}
		set
		{
			if (value != _ImageDecorate)
			{
				_ImageDecorate = value;
				OnPropertyChanged("ImageDecorate");
			}
		}
	}

	public IVmModule ModuleSource => VmSolution.Instance;

	public ICommand EditCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = _EditCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)EditCommandExecute);
				DelegateCommand val3 = val2;
				_EditCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand SelectCommand => (ICommand)(_SelectCommand ?? (_SelectCommand = new DelegateCommand<string>((Action<string>)SelectCommandExecute)));

	public event PropertyChangedEventHandler PropertyChanged;

	public ComprehensiveConfigControl()
	{
		InitializeComponent();
		Loaded += (object s, RoutedEventArgs e) =>
		{
			if (_parentWindow == null)
			{
				_parentWindow = Window.GetWindow(this);
				if (_parentWindow != null)
				{
					_parentWindow.Closing += OnParentWindowClosing;
					_parentWindow.Closing += OnClosing;
				}
				if (DataContext is GlobalRunParam globalRunParam)
				{
					globalRunParam.IsMultiRun = ((IVMRun)VmSolution.Instance).ContinuousRunEnable;
					globalRunParam.LoadData();
					globalRunParam.GlobalData.UpdateDecorateEvent += UpdateDecorate;
				}
			}
		};
		Unloaded += (object s, RoutedEventArgs e) =>
		{
			if (_isWindowClosing)
			{
				if (DataContext is GlobalRunParam globalRunParam)
				{
					globalRunParam.SaveData();
					globalRunParam.GlobalData.UpdateDecorateEvent -= UpdateDecorate;
					globalRunParam.GlobalData = null;
				}
				if (_parentWindow != null)
				{
					_parentWindow.Closing -= OnParentWindowClosing;
					_parentWindow.Closing -= OnClosing;
					_parentWindow = null;
				}
				_isWindowClosing = false;
			}
		};
	}

	private void OnParentWindowClosing(object sender, CancelEventArgs e)
	{
		_isWindowClosing = true;
	}

	private void OnPropertyChanged(string name)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	private void UpdateDecorate(IDecorate decorate)
	{
		if (DataContext is GlobalRunParam)
		{
			(DataContext as GlobalRunParam).ImageDecorate = decorate;
		}
	}

	private void SigRun_OnClick(object sender, RoutedEventArgs e)
	{
		SigRun();
	}

	private void MultiRun_OnClick(object sender, RoutedEventArgs e)
	{
		MutiRun();
	}

	private void StopRun_OnClick(object sender, RoutedEventArgs e)
	{
		StopRun();
	}

	private void Confirm_OnClick(object sender, RoutedEventArgs e)
	{
		if (Window.GetWindow(this) is TabWinBase)
		{
			Window.GetWindow(this)?.Close();
		}
	}

	private void OnClosing(object sender, CancelEventArgs e)
	{
		if (DataContext is GlobalRunParam)
		{
			(DataContext as GlobalRunParam).ImageDecorate = null;
		}
	}

	private void EditCommandExecute()
	{
		foreach (GlobalParamConfigItem globalParamItem in GlobalParamItems)
		{
			globalParamItem.Collection.Clear();
			if (globalParamItem.GroupCollection != null)
			{
				foreach (DynamicNameValueItemOfGroupCategory item3 in globalParamItem.GroupCollection)
				{
					globalParamItem.Collection.Add(ParamConfigDataBase.GetRunParamConfigDecorateForGroup(item3));
				}
			}
			foreach (DynamicNameValueItemOfGroup item in globalParamItem.ParamCollection)
			{
				RunParamConfigDecorate item2 = globalParamItem.Collection.LastOrDefault((RunParamConfigDecorate p) => p.GroupName == item.Group);
				int index = globalParamItem.Collection.IndexOf(item2);
				RunParamConfigDecorate runParamConfigDecorate = new RunParamConfigDecorate
				{
					DecorateSource = item,
					Owner = VmSolution.Instance
				};
				runParamConfigDecorate.DecorateSource.SetParentNode(globalParamItem);
				globalParamItem.Collection.Insert(index, runParamConfigDecorate);
			}
		}
		GlobalData.GlobalParamItems = GlobalParamItems;
		GlobalData.SelectedGlobalItem = GlobalParamItems.FirstOrDefault((GlobalParamConfigItem p) => p.IsSelected);
		GlobalParamConfigWindow globalParamConfigWindow = new GlobalParamConfigWindow
		{
			Owner = Window.GetWindow(this)
		};
		globalParamConfigWindow.DataContext = GlobalData;
		GlobalData.RelateControl = globalParamConfigWindow;
		GlobalData.AsyncLoadParams();
		globalParamConfigWindow.ShowDialog();
		GlobalData.UpdateParams();
		OnPropertyChanged("IsEmpty");
		OnPropertyChanged("GlobalOptions");
	}

	private void SelectCommandExecute(string param)
	{
		if (!string.IsNullOrEmpty(param) && (DataContext as GlobalRunParam).GlobalParamItems != null)
		{
			GlobalParamConfigItem globalParamConfigItem = (DataContext as GlobalRunParam).GlobalParamItems.FirstOrDefault((GlobalParamConfigItem p) => p.Name == param);
			if (globalParamConfigItem != null)
			{
				(DataContext as GlobalRunParam).SelectedGlobalItem = globalParamConfigItem;
			}
		}
	}

	private void SaveData()
	{
		string text = "";
		string text2 = "";
		for (int i = 0; i < GlobalParamItems.Count; i++)
		{
			if (i > 0)
			{
				text += ";";
				text2 += ";";
			}
			text += GlobalParamItems[i].Name;
			text2 += GlobalParamItems[i].ID;
		}
		VmSolution.Instance.UserData["GlobalParamNameKey"] = (string.IsNullOrEmpty(text) ? null : StringUtil.UTF8GetBytesPadZero(text));
		VmSolution.Instance.UserData["GlobalParamIdKey"] = (string.IsNullOrEmpty(text2) ? null : StringUtil.UTF8GetBytesPadZero(text2));
	}

	private void LoadData()
	{
		List<string> list = new List<string>();
		List<int> list2 = new List<int>();
		List<string> list3 = new List<string>();
		byte[] array = VmSolution.Instance.UserData["GlobalParamNameKey"];
		if (array != null)
		{
			list = StringUtil.UTF8GetString(array).Split(';').ToList();
		}
		byte[] array2 = VmSolution.Instance.UserData["GlobalParamIdKey"];
		if (array2 != null)
		{
			list2 = (from p in StringUtil.UTF8GetString(array2).Split(';')
				select int.Parse(p)).ToList();
		}
		int num = ((list.Count == list2.Count) ? list.Count : 0);
		if (num <= 0)
		{
			return;
		}
		for (int num2 = 0; num2 < num; num2++)
		{
			GlobalParamConfigItem globalParamConfigItem = new GlobalParamConfigItem();
			if (num2 == 0)
			{
				globalParamConfigItem.IsSelected = true;
			}
			globalParamConfigItem.Parent = globalParamConfigItem;
			globalParamConfigItem.Owner = VmSolution.Instance;
			globalParamConfigItem.GlobalData = GlobalData;
			globalParamConfigItem.Name = list[num2];
			globalParamConfigItem.ID = list2[num2];
			GlobalParamItems.Add(globalParamConfigItem);
		}
		OnPropertyChanged("IsEmpty");
		OnPropertyChanged("GlobalOptions");
	}

	private bool CheckRun()
	{
		IEnumerable<VmProcedureDecorator> enumerable = from p in VmSolution.Instance.Modules.OfType<VmProcedure>()
			select new VmProcedureDecorator
			{
				DecorateSource = p
			};
		VmProcedureDecorator vmProcedureDecorator = enumerable.FirstOrDefault((VmProcedureDecorator p) => p.WorkState != StatusBarSymbol.Ready && p.IsEnabled);
		if (vmProcedureDecorator != null)
		{
			string strMsg = vmProcedureDecorator.VMProcedure.Name + ":" + vmProcedureDecorator.WorkStateInfo;
			HikMessageBox.Show(strMsg, MessageIcons.Error);
			return false;
		}
		if (enumerable != null && enumerable.Where((VmProcedureDecorator p) => !p.IsEnabled).Count() == enumerable.Count())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("流程处于禁用状态"), MessageIcons.Warning);
			return false;
		}
		return true;
	}

	private void SigRun()
	{
		try
		{
			if (CheckRun())
			{
				VmSolution.Instance.Run();
			}
		}
		catch (VmException ex)
		{
			string strMsg = AppLocalizationService.GetLang("Lang_GlobalScript") + ":" + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			HikMessageBox.Show(strMsg, MessageIcons.Error);
		}
	}

	private void MutiRun()
	{
		try
		{
			if (CheckRun())
			{
				((IVMRun)VmSolution.Instance).ContinuousRunEnable = true;
				IsMultiRun = true;
			}
		}
		catch (VmException ex)
		{
			string strMsg = AppLocalizationService.GetLang("Lang_GlobalScript") + ":" + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			HikMessageBox.Show(strMsg, MessageIcons.Error);
		}
	}

	private void StopRun()
	{
		((IVMRun)VmSolution.Instance).ContinuousRunEnable = false;
		IsMultiRun = false;
	}

	public void SetParamTabVisible(string tabName, bool isVisible)
	{
		if (string.IsNullOrEmpty(tabName))
		{
			throw new VmException(-536870911);
		}
		GlobalParamConfigItem globalParamConfigItem = (DataContext as GlobalRunParam)?.GlobalParamItems.FirstOrDefault((GlobalParamConfigItem p) => p.Name == tabName);
		if (globalParamConfigItem != null)
		{
			globalParamConfigItem.IsVisible = isVisible;
			int selectedIndex = (DataContext as GlobalRunParam).GlobalParamItems.IndexOf(globalParamConfigItem);
			TabItemVisibleChanged(selectedIndex);
			(DataContext as GlobalRunParam).OnPropertyChanged("GlobalOptions");
			return;
		}
		throw new VmException(-536870911);
	}

	public void UpdateTabItemsVisibility(string tabName, bool isVisible)
	{
		foreach (object item in (IEnumerable)tabControl.Items)
		{
			if (item is GlobalParamConfigItem globalParamConfigItem && globalParamConfigItem.Name == tabName)
			{
				if (tabControl.ItemContainerGenerator.ContainerFromItem(item) is TabItem tabItem)
				{
					tabItem.Visibility = ((!isVisible) ? Visibility.Collapsed : Visibility.Visible);
					int selectedIndex = (DataContext as GlobalRunParam).GlobalParamItems.IndexOf(globalParamConfigItem);
					TabItemVisibleChanged(selectedIndex, isVisible);
				}
				break;
			}
		}
	}

	public List<string> GetParamTabNames()
	{
		List<string> list = new List<string>();
		try
		{
			foreach (GlobalParamConfigItem item in (DataContext as GlobalRunParam)?.GlobalParamItems)
			{
				list.Add(item.Name);
			}
		}
		catch (Exception)
		{
		}
		return list;
	}

	private void TabItemVisibleChanged(int selectedIndex)
	{
		if (selectedIndex < 0 || selectedIndex >= (DataContext as GlobalRunParam).GlobalParamItems.Count)
		{
			return;
		}
		GlobalParamConfigItem globalParamConfigItem = (DataContext as GlobalRunParam).GlobalParamItems[selectedIndex];
		if (globalParamConfigItem != null && !globalParamConfigItem.IsVisible)
		{
			if (tabControl.SelectedIndex >= 0 && tabControl.SelectedIndex < (DataContext as GlobalRunParam).GlobalParamItems.Count)
			{
				object selectedItem = tabControl.SelectedItem;
				if (selectedItem != null && selectedItem is GlobalParamConfigItem { IsVisible: false })
				{
					SelectNextVisibleTab(selectedIndex);
				}
			}
		}
		else if (globalParamConfigItem != null && globalParamConfigItem.IsVisible)
		{
			tabControl.SelectedIndex = selectedIndex;
		}
	}

	private void TabItemVisibleChanged(int selectedIndex, bool isVisible)
	{
		if (selectedIndex >= 0 && selectedIndex < (DataContext as GlobalRunParam).GlobalParamItems.Count)
		{
			GlobalParamConfigItem globalParamConfigItem = (DataContext as GlobalRunParam).GlobalParamItems[selectedIndex];
			if (globalParamConfigItem != null && !isVisible)
			{
				SelectNextVisibleTab(selectedIndex);
			}
			else if ((globalParamConfigItem != null) & isVisible)
			{
				tabControl.SelectedIndex = selectedIndex;
			}
		}
	}

	private void SelectNextVisibleTab(int currentIndex)
	{
		int num = -1;
		int count = (DataContext as GlobalRunParam).GlobalParamItems.Count;
		for (int i = currentIndex + 1; i < count; i++)
		{
			if ((DataContext as GlobalRunParam).GlobalParamItems[i].IsVisible)
			{
				num = i;
				break;
			}
		}
		if (num == -1)
		{
			for (int j = 0; j < currentIndex; j++)
			{
				if ((DataContext as GlobalRunParam).GlobalParamItems[j].IsVisible)
				{
					num = j;
					break;
				}
			}
		}
		if (num >= 0)
		{
			tabControl.SelectedIndex = num;
		}
		else
		{
			tabControl.SelectedIndex = 0;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}
}
