using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class QueueItemIn : QueueItemBase
{
	public const int TYPE_QUEUE = 0;

	private bool flagQueue = false;

	private bool flagQueueIndex = false;

	private bool flagFirstQueue = false;

	public string queueType = "int;float;string";

	public string oldQueueType = "int;float;string";

	private const string IMAGE_TYPE = "IMAGE";

	public Enumeration QueueItemList;

	private VmIO paramio = null;

	private DelegateCommand _DeleteCommand = null;

	private DelegateCommand _SelectedChangedCommand = null;

	private bool isThrowException = false;

	private int _nQueueIndex = -1;

	public Enumeration QueueItemListA
	{
		get
		{
			if (QueueItemList == null)
			{
				QueueItemList = new Enumeration();
				QueueItemList.Name = "QueueItemList";
				QueueItemList.AccessMode = "O";
				QueueItemList.CurValue = 0;
				QueueItemList.DefaultValue = 0;
				QueueItemList.SetParentNode(this);
			}
			if (!flagQueueIndex)
			{
				QueueItemList.SelectedChangedCommand = SelectedChangedCommand;
				flagQueueIndex = true;
			}
			if (GetParentNode(2u) is Tab tab)
			{
				BaseNode baseNode = tab.FindBaseNodeByName("DataCenter");
				if (baseNode != null && baseNode is EnumerationDataCenter)
				{
					DataQueue_SelectedChanged(baseNode, null);
					if (!flagQueue)
					{
						(baseNode as EnumerationDataCenter).OnSelectedChanged += DataQueue_SelectedChanged;
						flagQueue = true;
					}
				}
			}
			return QueueItemList;
		}
		set
		{
			QueueItemList = value;
			OnPropertyChanged("QueueItemListA");
		}
	}

	public override string Value => GenerateSetMsg();

	public override bool CustomVisibleA
	{
		get
		{
			return CustomVisible;
		}
		set
		{
			if (CustomVisible != value)
			{
				if (value)
				{
					CustomVisible = IsAddedA;
				}
				else
				{
					CustomVisible = value;
				}
				OnPropertyChanged("CustomVisibleA");
				ModifyDeleteBtnVisibility();
				ModifyDisplayIndex();
				if (!CustomVisible)
				{
					CurValueA = null;
				}
				if (ParamIO != null)
				{
					ParamIO.CustomVisibleA = CustomVisible;
				}
			}
		}
	}

	public override bool IsAddedA
	{
		get
		{
			return IsAdded;
		}
		set
		{
			if (IsAdded != value)
			{
				IsAdded = value;
				OnPropertyChanged("IsAddedA");
				SetParamUnit();
			}
		}
	}

	public VmIO ParamIO
	{
		get
		{
			if (paramio == null)
			{
				VmModule vmModule = GetVmModule();
				if (vmModule == null)
				{
					return null;
				}
				if (Triggers == null || Triggers.Count == 0)
				{
					return null;
				}
				if (!(Triggers[0] is Trigger) || (Triggers[0] as Trigger).Setters.Count == 0)
				{
					return null;
				}
				string operationParams = ((Triggers[0] as Trigger).Setters[0] as Setter).OperationParams;
				paramio = vmModule.GetIO(operationParams);
			}
			return paramio;
		}
	}

	public override ICommand DeleteCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_DeleteCommand == null)
			{
				_DeleteCommand = new DelegateCommand((Action)DeleteCommandCmdExecute);
			}
			return (ICommand)_DeleteCommand;
		}
	}

	public ICommand SelectedChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand((Action)SelectedChangedCmdExecute);
			}
			return (ICommand)_SelectedChangedCommand;
		}
	}

	private void DataQueue_SelectedChanged(object sender, EventArgs e)
	{
		if (QueueItemList == null || !CustomVisibleA || !(sender is EnumerationDataCenter { QueueList: var queueList }))
		{
			return;
		}
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (EnumEntry item in queueList)
		{
			observableCollection.Add(item);
		}
		QueueItemList.EnumEntrysA = observableCollection;
		if (e != null)
		{
			QueueItemList.CurValueA = 0;
		}
		if (_nQueueIndex > -1 && QueueItemList.CurValueA > 0)
		{
			if (QueueItemList.CurValueA > _nQueueIndex)
			{
				QueueItemList.CurValueA -= 1;
			}
			else if (QueueItemList.CurValueA == _nQueueIndex)
			{
				QueueItemList.CurValueA = 0;
			}
			if (OldValue != Value && SetParamUnit() == 0)
			{
				QueueItemList.OldValue = QueueItemList.Value;
			}
		}
		RefreshSendData();
	}

	public override void DeleteCommandCmdExecute()
	{
		if (!(GetParentNode() is Category { ItemsA: var itemsA }))
		{
			return;
		}
		QueueItemIn[] array = (from QueueItemIn p in itemsA.Where((object p) => p is QueueItemIn)
			where p.CustomVisibleA
			select p).ToArray();
		if (DisplayIndexA != array.Length)
		{
			for (int num = DisplayIndexA; num < array.Length; num++)
			{
				array[num - 1].IsAddedA = true;
				array[num - 1].CustomVisibleA = true;
				string text = array[num - 1].queueType;
				array[num - 1].queueType = array[num].queueType;
				array[num - 1].oldQueueType = array[num].oldQueueType;
				if (array[num].queueType.Equals("IMAGE") || text.Equals("IMAGE"))
				{
					array[num - 1].QueueItemList.CurValueA = array[num].QueueItemList.CurValueA;
					array[num - 1].CurValueA = array[num].CurValueA;
				}
				else
				{
					array[num - 1].CurValueA = array[num].CurValueA;
					array[num - 1].QueueItemList.CurValueA = array[num].QueueItemList.CurValueA;
				}
				array[num].CustomVisibleA = false;
			}
			array[array.Length - 1].CurValueA = null;
			array[array.Length - 1].QueueItemList.CurValueA = -1;
			array[array.Length - 1].IsAddedA = false;
			array[array.Length - 1].CustomVisibleA = false;
			array[array.Length - 1].DisplayIndexA = 0;
			array[array.Length - 1].queueType = "int;float;string";
			array[array.Length - 1].oldQueueType = "int;float;string";
		}
		else
		{
			IsAddedA = false;
			CustomVisibleA = false;
			CurValueA = null;
			if (QueueItemList != null)
			{
				QueueItemList.CurValue = 0;
			}
		}
		ModifyDisplayIndex();
		GetVmModule()?.OnPropertyChanged("IsReady");
	}

	private void SelectedChangedCmdExecute()
	{
		object topBaseNode = GetTopBaseNode();
		if (topBaseNode != null && OldValue != Value)
		{
			QueueItemList.OldValue = QueueItemList.Value;
			RefreshSendData();
			int num = SetParamUnit();
			if (num != 0)
			{
				QueueItemList.Value = QueueItemList.OldValue;
			}
		}
	}

	private void RefreshSendData()
	{
		if (QueueItemList.CurValueA == 0)
		{
			queueType = "";
		}
		else if (QueueItemList.CurValueA >= QueueItemList.EnumEntrysA.Count)
		{
			queueType = "";
			QueueItemList.CurValue = 0;
			SetParamUnit();
		}
		else
		{
			object obj = QueueItemList.EnumEntrysA.FirstOrDefault((object p) => (p as EnumEntry).ValueA == QueueItemList.CurValueA && (p as EnumEntry).ValueA > 0);
			if (obj != null && obj is EnumEntry)
			{
				queueType = (obj as EnumEntry).ReservedProperty.ToString();
			}
		}
		if (flagFirstQueue)
		{
			flagFirstQueue = false;
			oldQueueType = queueType;
		}
		UpdateOperationParams();
	}

	private void UpdateOperationParams()
	{
		DropOpenSetter.OperationParams = queueType;
		try
		{
			string text = "";
			string text2 = "";
			string text3 = "";
			if (queueType.Equals("IMAGE"))
			{
				if (ParamIO != null)
				{
					ParamIO.CustomVisibleA = false;
				}
				DropOpenSetter.OperationName = "GetFrontCombinationTreeOperation";
				DropOpenSetter.CreatOperateInstance();
				text = "InputImage" + DisplayIndex;
				text2 = "SetCombinationSourceOperation";
				text3 = "GetSelectedCombinationOperation";
			}
			else
			{
				if (ParamIO != null)
				{
					ParamIO.CustomVisibleA = true;
				}
				DropOpenSetter.OperationName = "GetFrontFilterTreeOperation";
				DropOpenSetter.CreatOperateInstance();
				text = "SendData" + DisplayIndex;
				text2 = "SetFilterSourceOperation";
				text3 = "GetSelectedFilterOperation";
			}
			for (int i = 0; i < Triggers.Count; i++)
			{
				if (!(Triggers[i] is Trigger trigger))
				{
					continue;
				}
				switch (i)
				{
				case 0:
					if (trigger.Setters[0] is Setter setter2)
					{
						setter2.OperationName = text2;
						setter2.OperationParams = text;
					}
					break;
				case 1:
					if (trigger.Setters[0] is Setter setter3)
					{
						setter3.OperationParams = text;
					}
					break;
				case 2:
					if (trigger.Setters[0] is Setter setter)
					{
						setter.OperationName = text3;
						setter.OperationParams = text;
					}
					break;
				}
			}
			(Initers[0] as Setter).OperationName = text3;
			(Initers[0] as Setter).OperationParams = text;
		}
		catch
		{
		}
		if (oldQueueType != queueType)
		{
			oldQueueType = queueType;
			DelSubscriber();
		}
		else if (!string.IsNullOrWhiteSpace(CurValueA))
		{
			if (string.IsNullOrWhiteSpace(queueType))
			{
				DelSubscriber();
			}
			else if (!CheckSubscribeDataType(CurValueA))
			{
				DelSubscriber();
			}
		}
	}

	private bool CheckSubscribeDataType(string filterStr)
	{
		bool result = true;
		if (string.IsNullOrWhiteSpace(filterStr))
		{
			return true;
		}
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return true;
			}
			if (Triggers == null || Triggers.Count == 0)
			{
				return result;
			}
			if (!(Triggers[0] is Trigger) || (Triggers[0] as Trigger).Setters.Count == 0)
			{
				return result;
			}
			if (!((Triggers[0] as Trigger).Setters[0] is Setter { OperationParamsA: var operationParamsA }))
			{
				return result;
			}
			VmIO iO = vmModule.GetIO(operationParamsA);
			if (iO == null)
			{
				return result;
			}
			if (iO.RelateIO != null)
			{
				if (iO.RelateIO is VmConstantIO)
				{
					return true;
				}
				string typeName = iO.RelateIO.TypeName;
				result = ((string.IsNullOrWhiteSpace(typeName) || !(typeName != queueType)) ? true : false);
			}
			else
			{
				result = false;
			}
		}
		catch (Exception)
		{
			result = true;
		}
		return result;
	}

	private void DelSubscriber()
	{
		SetPropertyValueByNameNotrigger("CurValueA", null);
		try
		{
			if (Triggers != null && Triggers.Count > 0)
			{
				if (!(Triggers[0] is Trigger) || (Triggers[0] as Trigger).Setters.Count == 0 || !((Triggers[0] as Trigger).Setters[0] is Setter setter))
				{
					return;
				}
				setter.Operate(new object[2] { this, null });
			}
			if (queueType.Equals("IMAGE"))
			{
				CurValueA = null;
			}
			isThrowException = false;
		}
		catch (Exception)
		{
			isThrowException = true;
		}
	}

	private void ReInniterBusindess()
	{
		if (Initers == null)
		{
			return;
		}
		foreach (Setter initer in Initers)
		{
			OperationManager.CreatInstanceBySetter(initer);
			initer.Operate(null);
		}
	}

	public override int GetParamUnit()
	{
		byte[] array = GetVmModule()?.UserData[NameA + ":DisplayIndexA"];
		if (array == null)
		{
			array = Encoding.UTF8.GetBytes("0");
		}
		DisplayIndex = int.Parse(StringUtil.UTF8GetString(array));
		MyItemState = ItemState.Normal;
		string text = string.Empty;
		VmModule vmModule = GetVmModule();
		int num = 0;
		try
		{
			if (vmModule != null)
			{
				text = vmModule.Params[Name];
			}
		}
		catch
		{
			num = -1;
		}
		if (num == 0)
		{
			string text2 = text;
			try
			{
				Check(text2);
				if (Value != text2)
				{
					if (Value.EndsWith("#0#0"))
					{
						flagFirstQueue = true;
					}
					OldValue = text2;
					AnalyseGetMsg(text2);
				}
			}
			catch (Exception ex)
			{
				string lang = AppLocalizationService.GetLang("Lang_ParamGet");
				HikMessageBox.Show(lang + ex.Message, MessageIcons.Error);
				LogHelper.Error("获取失败！！！！！名称：" + Name + " des:" + Description + "  获取值:" + text);
			}
			if (queueType.Equals("IMAGE"))
			{
				ReInniterBusindess();
			}
			return 0;
		}
		MyItemState = ItemState.GetParamErro;
		return -1;
	}

	public override int SetParamUnit()
	{
		string value = Value;
		if (string.IsNullOrWhiteSpace(value))
		{
			MyItemState = ItemState.SetParamErro;
			return -2;
		}
		MyItemState = ItemState.Normal;
		VmModule vmModule = GetVmModule();
		try
		{
			if (vmModule != null)
			{
				vmModule.Params[Name] = value;
			}
		}
		catch
		{
			MyItemState = ItemState.SetParamErro;
			OnSetValueCompleted(success: false);
			return -1;
		}
		OldValue = value;
		OnSetValueCompleted(success: true);
		SetQueueIndex();
		return 0;
	}

	private void SetQueueIndex()
	{
		string text = "";
		Tab tab = GetParentNode<Tab>();
		if (tab != null && tab.Collection != null)
		{
			foreach (NameValueItem item in tab.Collection)
			{
				if (item is QueueItemIn && item.CustomVisibleA)
				{
					text = text + (item as QueueItemIn).Value + "\r";
				}
			}
		}
		if (!string.IsNullOrEmpty(text))
		{
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
				vmModule.UserData["DataQueueRecord"] = StringUtil.UTF8GetBytesPadZero(text);
			}
		}
	}

	public override string ToString()
	{
		return Name + "_IsAdd[" + IsAddedA + "]_CustomVisible[" + CustomVisibleA + "]";
	}

	private string GenerateSetMsg()
	{
		string result = "";
		string text = Name + "#";
		try
		{
			if (Triggers == null || Triggers.Count == 0)
			{
				return result;
			}
			if (!(Triggers[0] is Trigger) || (Triggers[0] as Trigger).Setters.Count == 0)
			{
				return result;
			}
			text += ((Triggers[0] as Trigger).Setters[0] as Setter).OperationParams;
			text = ((!IsAddedA) ? (text + "#0") : (text + "#1"));
			text = ((QueueItemList != null) ? (text + "#" + QueueItemList.Value) : (text + "#0"));
			result = text;
		}
		catch (Exception)
		{
			return "";
		}
		return result;
	}

	private void AnalyseGetMsg(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return;
		}
		if (QueueItemList == null)
		{
			QueueItemList = new Enumeration();
			QueueItemList.Name = "QueueItemList";
			QueueItemList.AccessMode = "O";
			QueueItemList.CurValue = 0;
			QueueItemList.DefaultValue = 0;
			QueueItemList.SetParentNode(this);
		}
		if (!flagQueueIndex)
		{
			QueueItemList.SelectedChangedCommand = SelectedChangedCommand;
			flagQueueIndex = true;
		}
		string[] array = value.Split('#');
		if (array.Length < 4)
		{
			return;
		}
		IsAdded = array[2] == "1";
		QueueItemList.CurValue = Convert.ToInt32(array[3]);
		int num = -1;
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			BaseNode baseNode = algorithmTabRoot.FindBaseNodeByName("OutputType");
			if (baseNode != null && baseNode is RadioSelecterEx3)
			{
				num = (baseNode as RadioSelecterEx3).CurValueA;
			}
		}
		if (num == 0)
		{
			CustomVisibleA = array[2] == "1";
		}
		else
		{
			CustomVisibleA = false;
		}
		initOperationParam();
	}

	internal void RefreshDataSource(int delIndex)
	{
		if (GetParentNode(2u) is Tab tab)
		{
			BaseNode baseNode = tab.FindBaseNodeByName("DataCenter");
			if (baseNode != null && baseNode is EnumerationDataCenter)
			{
				_nQueueIndex = delIndex;
				DataQueue_SelectedChanged(baseNode, null);
				_nQueueIndex = -1;
			}
		}
	}

	private void initOperationParam()
	{
		if (GetParentNode(2u) is Tab tab)
		{
			BaseNode baseNode = tab.FindBaseNodeByName("DataCenter");
			if (baseNode != null && baseNode is EnumerationDataCenter)
			{
				DataQueue_SelectedChanged(baseNode, null);
			}
		}
	}
}
