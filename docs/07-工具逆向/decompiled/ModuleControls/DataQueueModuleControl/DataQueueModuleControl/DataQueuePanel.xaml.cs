using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace DataQueueModuleControl;

public partial class DataQueuePanel : UserControl, IComponentConnector, IStyleConnector
{
	private object _lock = new object();

	private IMVS_DATAQUEUE_LIST _curQueueList;

	private const string _prefix = "queue";

	private int _variableIndex;

	private bool init;

	private bool changeByget;

	private IVmModule _module;

	private IParamsConfig _param;

	public static readonly DependencyProperty ParentNodeProperty = DependencyProperty.Register("ParentNode", typeof(object), typeof(DataQueuePanel), new PropertyMetadata(null));

	public static readonly DependencyProperty CacheRowCountProperty = DependencyProperty.Register("CacheRowCount", typeof(int), typeof(DataQueuePanel), new PropertyMetadata(0, CallBack));

	public static readonly DependencyProperty ImageCacheCountProperty = DependencyProperty.Register("ImageCacheCount", typeof(int), typeof(DataQueuePanel), new PropertyMetadata(0, ImageCallBack));

	public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register("Items", typeof(ObservableCollection<DataQueueItem>), typeof(DataQueuePanel), new PropertyMetadata((object)null));

	public IVmModule Module
	{
		get
		{
			if (_module != null)
			{
				return _module;
			}
			object parentNode = ParentNode;
			if (parentNode == null)
			{
				return _module;
			}
			if (!(parentNode is NameValueItem nameValueItem))
			{
				return _module;
			}
			if (nameValueItem.GetTopBaseNode() is IOwner owner)
			{
				_module = owner.Owner as IVmModule;
			}
			return _module;
		}
	}

	public IParamsConfig Param
	{
		get
		{
			if (_param != null)
			{
				return _param;
			}
			object parentNode = ParentNode;
			if (parentNode == null)
			{
				return null;
			}
			if (!(parentNode is NameValueItem nameValueItem))
			{
				return null;
			}
			if (nameValueItem.GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
			{
				_param = algorithmTabRoot.UserStringData;
			}
			return _param;
		}
	}

	public object ParentNode
	{
		get
		{
			return GetValue(ParentNodeProperty);
		}
		set
		{
			SetValue(ParentNodeProperty, value);
		}
	}

	public int CacheRowCount
	{
		get
		{
			return (int)GetValue(CacheRowCountProperty);
		}
		set
		{
			SetValue(CacheRowCountProperty, value);
		}
	}

	public int ImageCacheCount
	{
		get
		{
			return (int)GetValue(ImageCacheCountProperty);
		}
		set
		{
			SetValue(ImageCacheCountProperty, value);
		}
	}

	public ObservableCollection<DataQueueItem> Items
	{
		get
		{
			return (ObservableCollection<DataQueueItem>)GetValue(ItemsProperty);
		}
		set
		{
			SetValue(ItemsProperty, value);
		}
	}

	public int MaxCount => 16;

	public DataQueuePanel()
	{
		InitializeComponent();
		Items = new ObservableCollection<DataQueueItem>();
		WeakEventManager<ObservableCollection<DataQueueItem>, NotifyCollectionChangedEventArgs>.AddHandler(Items, "CollectionChanged", OnCollectionChanged);
		Loaded += (object s, RoutedEventArgs e) =>
		{
			if (!init)
			{
				GetData();
				if (Param != null)
				{
					string empty = string.Empty;
					empty = ((IUserStringData)Param)["MaxRowNum"];
					CacheRowCount = int.Parse(empty);
					empty = ((IUserStringData)Param)["ImageCacheNum"];
					ImageCacheCount = int.Parse(empty);
				}
				init = true;
			}
		};
	}

	private static void ImageCallBack(DependencyObject sender, DependencyPropertyChangedEventArgs e)
	{
		if (sender is DataQueuePanel { Param: not null } dataQueuePanel)
		{
			int num = (int)e.NewValue;
			int num2 = (int)e.OldValue;
			if (num != num2 && dataQueuePanel.init)
			{
				((IUserStringData)dataQueuePanel.Param)["ImageCacheNum"] = num.ToString();
			}
		}
	}

	private static void CallBack(DependencyObject sender, DependencyPropertyChangedEventArgs e)
	{
		if (sender is DataQueuePanel { Param: not null } dataQueuePanel)
		{
			int num = (int)e.NewValue;
			int num2 = (int)e.OldValue;
			if (num != num2 && dataQueuePanel.init)
			{
				((IUserStringData)dataQueuePanel.Param)["MaxRowNum"] = num.ToString();
			}
		}
	}

	private void AddItem_OnClick(object sender, RoutedEventArgs e)
	{
		if (Items.Count >= MaxCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " :" + MaxCount, MessageButtons.Normal, MessageIcons.Warning);
			return;
		}
		AddNewItem(new DataQueueItem
		{
			Name = GetDefaultName(null),
			Type = "string",
			Value = new byte[131072]
		});
	}

	public string GetDefaultName(StringBuilder stringBuilder)
	{
		if (stringBuilder == null)
		{
			_variableIndex = 0;
			int capacity = 6;
			stringBuilder = new StringBuilder(capacity);
			stringBuilder.Append("queue");
			stringBuilder.Append(_variableIndex);
		}
		else
		{
			int num = _variableIndex;
			int num2 = 0;
			while (num > 0)
			{
				num /= 10;
				num2++;
			}
			if (num2 == 0)
			{
				num2 = 1;
			}
			stringBuilder.Remove("queue".Length, num2);
			_variableIndex++;
			stringBuilder.Append(_variableIndex);
		}
		if (!Items.Any((DataQueueItem p) => p.Name == stringBuilder.ToString()))
		{
			return stringBuilder.ToString();
		}
		return GetDefaultName(stringBuilder);
	}

	private void GetData()
	{
		if (Param == null)
		{
			return;
		}
		lock (_lock)
		{
			changeByget = true;
			IntPtr intPtr = IntPtr.Zero;
			byte[] array = ((IUserBytesData)Param)["DataQueueList"];
			if (array != null && array.Length != 0)
			{
				intPtr = Marshal.AllocHGlobal(array.Length);
				Marshal.Copy(array, 0, intPtr, array.Length);
				IMVS_DATAQUEUE_LIST iMVS_DATAQUEUE_LIST = Marshal.PtrToStructure<IMVS_DATAQUEUE_LIST>(intPtr);
				if (!_curQueueList.Equals(iMVS_DATAQUEUE_LIST))
				{
					Items.Clear();
					for (int i = 0; i < iMVS_DATAQUEUE_LIST.nCount; i++)
					{
						IMVS_DATAQUEUE value = iMVS_DATAQUEUE_LIST.stDataQueue[i];
						AddNewItem(DataQueueUtil.StructToItem(value));
					}
					_curQueueList = iMVS_DATAQUEUE_LIST;
				}
			}
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
			}
			changeByget = false;
		}
	}

	private void AddNewItem(DataQueueItem item)
	{
		Items.Add(item);
		item.Parent = Items;
		WeakEventManager<DataQueueItem, PropertyChangedEventArgs>.AddHandler(item, "PropertyChanged", ValueChanged);
	}

	private void Delete_OnHandler(object sender, RoutedEventArgs e)
	{
		if (sender is Button { DataContext: DataQueueItem dataContext })
		{
			dataContext.Parent = null;
			int index = dataContext.Index;
			WeakEventManager<DataQueueItem, PropertyChangedEventArgs>.RemoveHandler(dataContext, "PropertyChanged", ValueChanged);
			if (Items.Contains(dataContext))
			{
				Items.Remove(dataContext);
			}
			ChangeToSendReceiveData(index);
		}
	}

	private void UpdateSendDatas(VmModule sendModule, int delIndex)
	{
		try
		{
			if (sendModule.Params["OutputType"] != "0")
			{
				return;
			}
			string text = sendModule.Params["DataCenter"];
			if (text.Substring(0, text.LastIndexOf('+')) != Module.ID.ToString())
			{
				return;
			}
			byte[] array = sendModule.UserData["DataQueueRecord"];
			if (array == null || array.Length == 0)
			{
				return;
			}
			string text2 = StringUtil.UTF8GetString(array);
			string[] array2 = text2.Split('\r');
			if (array2.Length == 0)
			{
				return;
			}
			for (int i = 0; i < array2.Length; i++)
			{
				if (string.IsNullOrEmpty(array2[i]))
				{
					continue;
				}
				string[] array3 = array2[i].Split('#');
				string key = array3[0];
				string name = array3[1];
				int num = int.Parse(array3[3]);
				if (delIndex > num)
				{
					continue;
				}
				if (delIndex < num)
				{
					num--;
				}
				else if (delIndex == num)
				{
					num = 0;
					VmIO iO = sendModule.GetIO(name);
					if (iO != null)
					{
						iO.RelateIO = null;
					}
				}
				array3[3] = num.ToString();
				string text3 = string.Join("#", array3);
				sendModule.Params[key] = text3;
				array2[i] = text3;
			}
			string text4 = string.Join("\r", array2);
			if (text2 != text4)
			{
				sendModule.UserData["DataQueueRecord"] = StringUtil.UTF8GetBytesPadZero(text4);
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error("UpdateSendDatas is error" + ex.Message);
		}
	}

	private void UpdateReceiveDatas(VmModule receiveModule, int delIndex)
	{
		try
		{
			if (receiveModule.Params["OutputType"] != "0")
			{
				return;
			}
			string text = receiveModule.Params["DataCenter"];
			if (text.Substring(0, text.LastIndexOf('+')) != Module.ID.ToString())
			{
				return;
			}
			byte[] array = receiveModule.UserData["DataQueueRecord"];
			if (array == null || array.Length == 0)
			{
				return;
			}
			string text2 = StringUtil.UTF8GetString(array);
			if (string.IsNullOrEmpty(text2))
			{
				return;
			}
			string[] array2 = text2.Split('\n');
			for (int i = 0; i < array2.Length; i++)
			{
				if (!string.IsNullOrEmpty(array2[i]))
				{
					string[] array3 = array2[i].Split('\r');
					int num = int.Parse(array3[1]);
					if (num > delIndex)
					{
						num--;
					}
					else if (num == delIndex)
					{
						num = 0;
					}
					receiveModule.UserData[array3[0]] = StringUtil.UTF8GetBytesPadZero(num.ToString());
					array3[1] = num.ToString();
					array2[i] = string.Join("\r", array3);
					receiveModule.Params["ModifyQueueIndex"] = array2[i];
				}
			}
			string text3 = string.Join("\n", array2);
			if (text2 != text3)
			{
				receiveModule.UserData["DataQueueRecord"] = StringUtil.UTF8GetBytesPadZero(text3);
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error("UpdateReceiveDatas is error" + ex.Message);
		}
	}

	private void ChangeToSendReceiveData(int delIndex)
	{
		List<VmModule> list = new List<VmModule>();
		VmSolution.Instance.GetAllModuleByType(list, "SendDatasModule");
		VmSolution.Instance.GetAllModuleByType(list, "ReadDatasModule");
		foreach (VmModule item in list)
		{
			if (item.StrModuleName == "SendDatasModule")
			{
				UpdateSendDatas(item, delIndex);
			}
			else if (item.StrModuleName == "ReadDatasModule")
			{
				UpdateReceiveDatas(item, delIndex);
			}
		}
	}

	private void ChangeSendModuleIo(int nchangeindex)
	{
		List<VmModule> list = new List<VmModule>();
		VmSolution.Instance.GetAllModuleByType(list, "SendDatasModule");
		foreach (VmModule item in list)
		{
			ChangeSendSub(item, nchangeindex);
		}
	}

	private void ChangeSendSub(VmModule sendModule, int delIndex)
	{
		try
		{
			if (sendModule.Params["OutputType"] != "0")
			{
				return;
			}
			string text = sendModule.Params["DataCenter"];
			if (text.Substring(0, text.LastIndexOf('+')) != Module.ID.ToString())
			{
				return;
			}
			byte[] array = sendModule.UserData["DataQueueRecord"];
			if (array == null || array.Length == 0)
			{
				return;
			}
			string[] array2 = StringUtil.UTF8GetString(array).Split('\r');
			if (array2.Length == 0)
			{
				return;
			}
			for (int i = 0; i < array2.Length; i++)
			{
				if (string.IsNullOrEmpty(array2[i]))
				{
					continue;
				}
				string[] array3 = array2[i].Split('#');
				_ = array3[0];
				string name = array3[1];
				int num = int.Parse(array3[3]);
				if (delIndex == num)
				{
					VmIO iO = sendModule.GetIO(name);
					if (iO != null)
					{
						iO.RelateIO = null;
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error("UpdateSendDatas is error" + ex.Message);
		}
	}

	private void ValueChanged(object sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if (propertyName == "Index")
		{
			return;
		}
		if (propertyName == "Name")
		{
			ModifyName(sender as DataQueueItem);
			return;
		}
		if (e.PropertyName == "Type")
		{
			ChangeSendModuleIo((sender as DataQueueItem).Index);
		}
		SetData();
	}

	private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.Action != NotifyCollectionChangedAction.Reset)
		{
			SetData();
			for (int i = 0; i < Items.Count; i++)
			{
				Items[i].Index = i + 1;
			}
		}
	}

	private void ModifyName(DataQueueItem item)
	{
		if (Param == null || item == null)
		{
			return;
		}
		lock (_lock)
		{
			if (!changeByget)
			{
				((IUserStringData)Param)[$"ModifyName{item.Index}"] = item.Name;
				DataQueueUtil.bNameChanged = true;
			}
		}
	}

	private void SetData()
	{
		if (Param == null)
		{
			return;
		}
		lock (_lock)
		{
			if (!changeByget)
			{
				IMVS_DATAQUEUE_LIST structure = new IMVS_DATAQUEUE_LIST
				{
					nCount = Items.Count,
					nReserved = new int[4],
					stDataQueue = new IMVS_DATAQUEUE[16]
				};
				for (int i = 0; i < Items.Count && i < structure.stDataQueue.Length; i++)
				{
					structure.stDataQueue[i] = Items[i];
				}
				int num = Marshal.SizeOf<IMVS_DATAQUEUE_LIST>();
				IntPtr intPtr = Marshal.AllocHGlobal(num);
				Marshal.StructureToPtr(structure, intPtr, fDeleteOld: false);
				byte[] array = new byte[num];
				Marshal.Copy(intPtr, array, 0, num);
				((IUserBytesData)Param)["DataQueueList"] = array;
				Marshal.FreeHGlobal(intPtr);
				intPtr = IntPtr.Zero;
			}
		}
	}

	private void Source_MouseDown(object sender, MouseButtonEventArgs e)
	{
		controlitem.Focus();
	}
}
