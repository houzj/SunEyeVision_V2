using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using Mvvm.Commands;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace DynamicObjectConfigControl;

public partial class DynamicResultView : Control, INotifyPropertyChanged, IComponentConnector
{
	private const string DynamicOutputListKey = "DynamicOutputListKey";

	private ResultChooseData _ResultChooseData;

	private List<DynamicOutputData> _DynamicOutputList;

	public static readonly DependencyProperty ImageDecorateProperty = DependencyProperty.Register("ImageDecorate", typeof(IDecorate), typeof(DynamicResultView), new PropertyMetadata(null));

	public static readonly DependencyProperty ModuleSourceProperty = DependencyProperty.Register("ModuleSource", typeof(IVmModule), typeof(DynamicResultView), new PropertyMetadata(null, ModuleSourceChanged));

	public static readonly DependencyProperty DynamicOutputListProperty = DependencyProperty.Register("DynamicOutputList", typeof(ObservableCollection<DynamicOutputData>), typeof(DynamicResultView), new PropertyMetadata(new ObservableCollection<DynamicOutputData>()));

	private DelegateCommand _FiltrateCommand = null;

	public static readonly DependencyProperty IsButtomCombinationVisibleProperty = DependencyProperty.Register("IsButtomCombinationVisible", typeof(bool), typeof(DynamicResultView), new PropertyMetadata(true, OnButtomCombinationVisibleChanged));

	public IDecorate ImageDecorate
	{
		get
		{
			return (IDecorate)GetValue(ImageDecorateProperty);
		}
		set
		{
			SetValue(ImageDecorateProperty, value);
		}
	}

	public IVmModule ModuleSource
	{
		get
		{
			return (IVmModule)GetValue(ModuleSourceProperty);
		}
		set
		{
			SetValue(ModuleSourceProperty, value);
		}
	}

	public ObservableCollection<DynamicOutputData> DynamicOutputList
	{
		get
		{
			return (ObservableCollection<DynamicOutputData>)GetValue(DynamicOutputListProperty);
		}
		set
		{
			SetValue(DynamicOutputListProperty, value);
		}
	}

	public ResultChooseData ResultChooseData
	{
		get
		{
			if (_ResultChooseData == null)
			{
				_ResultChooseData = new ResultChooseData
				{
					ModuleSource = ModuleSource
				};
			}
			return _ResultChooseData;
		}
	}

	public double TabItemWidth => (AppLocalizationService.GetLang("Language") == "English") ? 110 : 76;

	public ICommand FiltrateCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = _FiltrateCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)FiltrateCommandExecute);
				DelegateCommand val3 = val2;
				_FiltrateCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public bool IsButtomCombinationVisible
	{
		get
		{
			return (bool)GetValue(IsButtomCombinationVisibleProperty);
		}
		set
		{
			SetValue(IsButtomCombinationVisibleProperty, value);
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public DynamicResultView()
	{
		InitializeComponent();
		Loaded += DynamicResultView_Loaded;
		Unloaded += DynamicResultView_Unloaded;
	}

	public virtual void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	private void DynamicResultView_Loaded(object sender, RoutedEventArgs e)
	{
		if (ModuleSource is IVmSolution)
		{
			foreach (DynamicOutputData dynamicOutput in DynamicOutputList)
			{
				if (dynamicOutput.IoSource != null)
				{
					((IVmModule)dynamicOutput.IoSource.Root).SubscribeCount++;
				}
			}
		}
		ServerSDKManager.serverSDKManager.mModuleManager.OnModuleResultCallBack -= ModuleResultCallBack;
		ServerSDKManager.serverSDKManager.mModuleManager.OnModuleResultCallBack += ModuleResultCallBack;
	}

	private void DynamicResultView_Unloaded(object sender, RoutedEventArgs e)
	{
		if (ModuleSource is IVmSolution)
		{
			foreach (DynamicOutputData dynamicOutput in DynamicOutputList)
			{
				if (dynamicOutput.IoSource != null)
				{
					((IVmModule)dynamicOutput.IoSource.Root).SubscribeCount--;
				}
			}
		}
		ServerSDKManager.serverSDKManager.mModuleManager.OnModuleResultCallBack -= ModuleResultCallBack;
	}

	public static void ModuleSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		DynamicResultView dynamicResultView = (DynamicResultView)d;
		dynamicResultView.LoadData();
		dynamicResultView.GetLatestResult();
	}

	private void ModuleResultCallBack(ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX_Data result)
	{
		Dispatcher?.BeginInvoke((Action)(() =>
		{
			GetRealResult(result);
		}));
	}

	private void FiltrateCommandExecute()
	{
		ResultChooseWindow resultChooseWindow = new ResultChooseWindow
		{
			Owner = Window.GetWindow(this)
		};
		_ResultChooseData = null;
		LoadData();
		resultChooseWindow.DataContext = ResultChooseData;
		resultChooseWindow.ShowDialog();
		List<VmIO> list = new List<VmIO>();
		foreach (Holder item2 in ResultChooseData.EnumEntrysA.OfType<Holder>())
		{
			list.AddRange(GetAllSelectedIO(item2));
		}
		if (ModuleSource is IVmSolution)
		{
			foreach (DynamicOutputData dynamicOutput in DynamicOutputList)
			{
				if (dynamicOutput.IoSource != null)
				{
					((IVmModule)dynamicOutput.IoSource.Root).SubscribeCount--;
				}
			}
		}
		ObservableCollection<DynamicOutputData> dynamicOutputList = new ObservableCollection<DynamicOutputData>();
		list.ForEach((VmIO p) =>
		{
			DynamicOutputData item = new DynamicOutputData
			{
				IoSource = p,
				Name = p.Root.UniqueName + "." + AppLocalizationService.GetLang(p.UniqueName)
			};
			if (ModuleSource is IVmSolution)
			{
				((IVmModule)p.Root).SubscribeCount++;
			}
			dynamicOutputList.Add(item);
		});
		DynamicOutputList = dynamicOutputList;
		GetLatestResult();
		SaveData();
	}

	private List<VmIO> GetAllSelectedIO(Holder holder, List<VmIO> ioList = null)
	{
		if (ioList == null)
		{
			ioList = new List<VmIO>();
		}
		if (holder != null)
		{
			if (holder.EnumEntrysA != null && holder.EnumEntrysA.Count > 0)
			{
				holder.EnumEntrysA.OfType<Holder>().ToList().ForEach((Holder p) =>
				{
					GetAllSelectedIO(p, ioList);
				});
			}
			else if (holder.CheckEnabled.Value)
			{
				ioList.Add(holder.HolderTarget as VmIO);
			}
		}
		return ioList;
	}

	private void SaveData()
	{
		string text = string.Join(";", DynamicOutputList.Select((DynamicOutputData p) => p.IoSource.ModulePathName));
		if (ModuleSource != null)
		{
			ModuleSource.UserData["DynamicOutputListKey"] = (string.IsNullOrEmpty(text) ? null : StringUtil.UTF8GetBytesPadZero(text));
		}
	}

	private void LoadData()
	{
		if (ModuleSource == null)
		{
			return;
		}
		List<string> list = new List<string>();
		byte[] array = ModuleSource.UserData["DynamicOutputListKey"];
		if (array != null)
		{
			list = StringUtil.UTF8GetString(array).Split(';').ToList();
		}
		if (list.Count <= 0)
		{
			return;
		}
		ObservableCollection<DynamicOutputData> observableCollection = new ObservableCollection<DynamicOutputData>();
		foreach (string item in list)
		{
			string[] arrName = item.Split(' ', '.');
			if (arrName.Length != 3)
			{
				continue;
			}
			VmIO vmIO = null;
			if (ModuleSource is IVmSolution)
			{
				Holder holder = ResultChooseData.EnumEntrysA.OfType<Holder>().FirstOrDefault((Holder p) => (p.HolderTarget as VmProcedure)?.ID.ToString() == arrName[0]);
				if (holder != null)
				{
					vmIO = GetSelectedIO(holder, arrName[2]);
				}
			}
			else
			{
				foreach (Holder item2 in ResultChooseData.EnumEntrysA.OfType<Holder>())
				{
					vmIO = GetSelectedIO(item2, arrName[2]);
					if (vmIO != null)
					{
						break;
					}
				}
			}
			if (vmIO != null)
			{
				DynamicOutputData dynamicOutputData = new DynamicOutputData();
				dynamicOutputData.IoSource = vmIO;
				dynamicOutputData.Name = vmIO.Root.UniqueName + "." + AppLocalizationService.GetLang(vmIO.UniqueName);
				observableCollection.Add(dynamicOutputData);
			}
		}
		DynamicOutputList = observableCollection;
	}

	private VmIO GetSelectedIO(Holder holder, string name)
	{
		VmIO vmIO = null;
		if (holder != null && !string.IsNullOrEmpty(name))
		{
			if (holder.EnumEntrysA != null && holder.EnumEntrysA.Count > 0)
			{
				foreach (Holder item in holder.EnumEntrysA.OfType<Holder>().ToList())
				{
					vmIO = GetSelectedIO(item, name);
					if (vmIO != null)
					{
						break;
					}
				}
			}
			else if (AppLocalizationService.GetLang(holder.Name) == name)
			{
				holder.CheckEnabled = true;
				vmIO = holder.HolderTarget as VmIO;
			}
		}
		return vmIO;
	}

	private void GetLatestResult()
	{
		try
		{
			foreach (DynamicOutputData dynamicOutput in DynamicOutputList)
			{
				VmIO ioSource = dynamicOutput.IoSource;
				if (ioSource != null)
				{
					dynamicOutput.Value = GetIOCurrentValue(ioSource.Value as Array, ioSource.TypeName);
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("GetLatestResult error: " + ex.ToString());
		}
	}

	private void GetRealResult(ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_LIST_EX_Data dataResult)
	{
		try
		{
			foreach (DynamicOutputData dynamicOutput in DynamicOutputList)
			{
				VmIO ioSource = dynamicOutput.IoSource;
				if (ioSource != null && ioSource.Root?.ID == dataResult.nModuleID)
				{
					dynamicOutput.Value = string.Join(";", VmIOManager.GetLeafIOResultByCallBack(dataResult, ioSource));
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("GetRealResult error: " + ex.ToString());
		}
	}

	private string GetIOCurrentValue(Array array, string typeName)
	{
		string result = "";
		ObservableCollection<string> observableCollection = new ObservableCollection<string>();
		if (array != null)
		{
			foreach (object item2 in array)
			{
				if (item2 == null)
				{
					continue;
				}
				if (typeName == "int" || typeName == "float")
				{
					observableCollection.Add(item2.ToString());
				}
				else if (typeName == "string")
				{
					if (item2 is ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX)
					{
						observableCollection.Add(((ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX)item2).strValue);
					}
					else if (item2 is string item)
					{
						observableCollection.Add(item);
					}
				}
				else if (typeName == "byte" && item2 is ImvsSdkDefine.IMVS_MODULE_BYTEDATA_VALUE_EX_Data iMVS_MODULE_BYTEDATA_VALUE_EX_Data)
				{
					observableCollection.Add(iMVS_MODULE_BYTEDATA_VALUE_EX_Data.strValue);
				}
			}
		}
		if (observableCollection.Count > 0)
		{
			result = string.Join(";", observableCollection);
		}
		return result;
	}

	private static void OnButtomCombinationVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		DynamicResultView dynamicResultView = d as DynamicResultView;
		bool newValue = (bool)e.NewValue;
		dynamicResultView?.UpdateRowHeight(newValue);
	}

	private void UpdateRowHeight(bool newValue)
	{
		if (!(Template.FindName("LayoutRoot", this) is Grid grid) || grid.RowDefinitions.Count <= 0)
		{
			return;
		}
		foreach (RowDefinition item in (IEnumerable<RowDefinition>)grid.RowDefinitions)
		{
			if (item.Name == "rowInput")
			{
				item.Height = (newValue ? new GridLength(188.0) : new GridLength(0.0));
			}
		}
	}
}
