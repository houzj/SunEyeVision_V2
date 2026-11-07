using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class DataSortConfig : NameValueItem
{
	public ObservableCollection<SortItem> _SortItems;

	public int _SelectTypeIndex;

	public ObservableCollection<string> PointTypeSource = new ObservableCollection<string>
	{
		AppLocalizationService.GetLang("Clockwise"),
		AppLocalizationService.GetLang("Anticlockwise")
	};

	public ObservableCollection<string> FloatTypeSource = new ObservableCollection<string>
	{
		AppLocalizationService.GetLang("Lang_顺序"),
		AppLocalizationService.GetLang("Lang_倒序")
	};

	private ICommand _AddSortItemCommand = null;

	private DelegateCommand<object> _PreviewMouseWheelCommand = null;

	private bool hasRegistered;

	public ObservableCollection<SortItem> SortItems
	{
		get
		{
			if (_SortItems == null)
			{
				_SortItems = new ObservableCollection<SortItem>();
			}
			return _SortItems;
		}
		set
		{
			if (_SortItems != value)
			{
				_SortItems = value;
				OnPropertyChanged("SortItems");
			}
		}
	}

	public int SelectTypeIndex
	{
		get
		{
			return _SelectTypeIndex;
		}
		set
		{
			if (_SelectTypeIndex != value)
			{
				_SelectTypeIndex = value;
				OnPropertyChanged("SelectTypeIndex");
			}
		}
	}

	public ICommand AddSortItemCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_AddSortItemCommand == null)
			{
				_AddSortItemCommand = (ICommand)new DelegateCommand((Action)AddSortItemCmdExecute);
			}
			return _AddSortItemCommand;
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

	public void AddSortItemCmdExecute()
	{
		if (Enumerable.Count(SortItems) >= 10)
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
		string sortName = AppLocalizationService.GetLang("Lang_Sort") + i;
		SortItem sortItem = new SortItem();
		sortItem.SortID = i;
		SortItem sortItem2 = sortItem;
		string typeName;
		if (SelectTypeIndex != 0)
		{
			typeName = ((SelectTypeIndex == 1) ? "float" : "point");
		}
		else
		{
			typeName = "int";
		}
		sortItem2.TypeName = typeName;
		sortItem.SortName = sortName;
		sortItem.vmModule = vmModule;
		sortItem.owner = this;
		sortItem.SetParentNode(this);
		SortItems.Add(sortItem);
		SetSortConfig();
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

	public bool CheckGroupName(int seq)
	{
		foreach (SortItem sortItem in SortItems)
		{
			if (sortItem.SortID == seq)
			{
				return false;
			}
		}
		return true;
	}

	public void DeleteAllItem()
	{
		SortItems.Clear();
		SetSortParam();
		SetSortConfig();
	}

	public void SetSortParam()
	{
		try
		{
			string text = "";
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			DataSortParam dataSortParam = ((vmModule.Params.Data as AlgorithmTabRoot).Tabs[0] as Tab).Collection[0] as DataSortParam;
			foreach (object item in dataSortParam.ItemsA)
			{
				VmIODecorate vmIODecorate = item as VmIODecorate;
				string text2 = $"{vmIODecorate.Name}:{vmIODecorate.ValueType}:{vmIODecorate.SortSelecter?.CurValue};";
				text += text2;
			}
			((IUserBytesData)vmModule.Params)["InputText"] = StringUtil.UTF8GetBytesPadZero(text);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	public void SetSortConfig()
	{
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			string text = "";
			foreach (SortItem sortItem in SortItems)
			{
				string text2 = $"%{sortItem.SortSelecter?.Value}%:{sortItem.SelectedSortTypeIndex}:{sortItem.SortID}:{sortItem.TypeName};";
				text += text2;
			}
			((IUserBytesData)vmModule.Params)["SortText"] = StringUtil.UTF8GetBytesPadZero(text);
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}
}
