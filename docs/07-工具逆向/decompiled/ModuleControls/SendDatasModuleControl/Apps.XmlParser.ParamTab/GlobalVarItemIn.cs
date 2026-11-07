using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.Variable;
using Apps.XmlUI.ParamTab;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GlobalVarItemIn : GlobalVarItemBase
{
	public const int TYPE_GLOBALVAR = 2;

	private bool flagQueue = false;

	private bool flagGlobalVarIndex = false;

	private bool flagFirstQueue = true;

	public string globalVarType = "int;float;string";

	public string oldGlobalVarType = "int;float;string";

	public EnumerationVar GlobalVarItemList;

	private DelegateCommand _DeleteCommand = null;

	private DelegateCommand _SelectedChangedCommand = null;

	public int DisplayIndex;

	private bool isThrowException = false;

	private VmIO paramio = null;

	public static int ModuNodeID { get; set; }

	public static string ModuDisplayName { get; set; }

	public static string ModuName { get; set; }

	public string CommunicationAddress
	{
		get
		{
			if (GlobalVarItemList != null)
			{
				object reservedProperty = GlobalVarItemList.ReservedProperty;
				if (reservedProperty != null && reservedProperty is IVmModule vmModule)
				{
					return vmModule.ID + "+" + vmModule.ModuleTypeName;
				}
				return "";
			}
			return "";
		}
	}

	public EnumerationVar GlobalVarItemListA
	{
		get
		{
			if (GlobalVarItemList == null)
			{
				GlobalVarItemList = new EnumerationVar();
				GlobalVarItemList.Name = "GlobalVarItemList";
				GlobalVarItemList.AccessMode = "O";
				GlobalVarItemList.CurValue = 0;
				GlobalVarItemList.DefaultValue = 0;
				GlobalVarItemList.SetParentNode(this);
			}
			if (!flagGlobalVarIndex)
			{
				GlobalVarItemList.SelectedChangedCommand = SelectedChangedCommand;
				flagGlobalVarIndex = true;
			}
			GetGlobalVarList(-1);
			return GlobalVarItemList;
		}
		set
		{
			GlobalVarItemList = value;
			OnPropertyChanged("GlobalVarItemListA");
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

	public int DisplayIndexA
	{
		get
		{
			return DisplayIndex;
		}
		set
		{
			DisplayIndex = value;
			OnPropertyChanged("DisplayIndexA");
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
				vmModule.UserData[NameA + ":DisplayIndexA"] = StringUtil.UTF8GetBytesPadZero(DisplayIndex.ToString());
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

	public void GetGlobalVarList(int GlobalItemIndex)
	{
		if (GlobalVarItemList == null)
		{
			return;
		}
		IList slectGlobalVar = GlobalVarHelper.GetSlectGlobalVar(GetVmModule());
		ObservableCollection<object> observableCollection = new ObservableCollection<object>();
		foreach (EnumEntry item in slectGlobalVar)
		{
			item.OnItemClicked -= TmpEntry_OnItemClicked;
			item.OnItemClicked += TmpEntry_OnItemClicked;
			observableCollection.Add(item);
		}
		GlobalVarItemList.EnumEntrysA = observableCollection;
		if (GlobalItemIndex > -1 && GlobalVarItemList.CurValueA > 0)
		{
			if (GlobalVarItemList.CurValueA > GlobalItemIndex)
			{
				GlobalVarItemList.CurValueA -= 1;
			}
			else if (GlobalVarItemList.CurValueA == GlobalItemIndex)
			{
				GlobalVarItemList.Value = "0";
				GlobalVarItemListA.CurrentNameA = AppLocalizationService.GetLang("Lang_PlzChoose");
			}
			if (OldValue != Value && SetParamUnit() == 0)
			{
				GlobalVarItemList.OldValue = GlobalVarItemList.Value;
			}
		}
		RefreshSendData();
		if (GlobalVarItemList.EnumEntrysA.Count > 0 && string.IsNullOrEmpty(GlobalVarItemList.CurrentNameA))
		{
			GlobalVarItemList.CurrentNameA = AppLocalizationService.GetLang("Lang_PlzChoose");
		}
	}

	public override void DeleteCommandCmdExecute()
	{
		if (!(GetParentNode() is Category { ItemsA: var itemsA }))
		{
			return;
		}
		GlobalVarItemIn[] array = (from GlobalVarItemIn p in itemsA.Where((object p) => p is GlobalVarItemIn)
			where p.CustomVisibleA
			select p).ToArray();
		if (DisplayIndexA != array.Length)
		{
			for (int num = DisplayIndexA; num < array.Length; num++)
			{
				array[num - 1].globalVarType = array[num].globalVarType;
				array[num - 1].oldGlobalVarType = array[num].oldGlobalVarType;
				array[num - 1].DropOpenSetter.OperationParams = array[num].globalVarType;
				array[num - 1].CurValueA = array[num].CurValueA;
				array[num - 1].GlobalVarItemList.CurValueA = array[num].GlobalVarItemList.CurValueA;
				array[num - 1].GlobalVarItemList.CurrentNameA = array[num].GlobalVarItemList.CurrentNameA;
				array[num - 1].IsAddedA = true;
				array[num - 1].CustomVisibleA = true;
				array[num - 1].SetParamUnit();
				array[num].CurValueA = null;
				array[num].GlobalVarItemList.CurValueA = 0;
				array[num].GlobalVarItemList.CurrentNameA = "";
				array[num].IsAddedA = false;
				array[num].CustomVisibleA = false;
				array[num].DisplayIndexA = 0;
				array[num].globalVarType = "int;float;string";
				array[num].oldGlobalVarType = "int;float;string";
				array[num].DropOpenSetter.OperationParams = array[num].globalVarType;
			}
		}
		else
		{
			IsAddedA = false;
			CustomVisibleA = false;
			CurValueA = null;
			if (GlobalVarItemList != null)
			{
				GlobalVarItemList.Value = "0";
				if (GlobalVarItemList.EnumEntrysA.Count > 0)
				{
					GlobalVarItemList.CurrentNameA = ButtonSelecter.strChoice;
				}
				else
				{
					GlobalVarItemList.CurrentNameA = "";
				}
			}
		}
		ModifyDisplayIndex();
		GetVmModule()?.OnPropertyChanged("IsReady");
	}

	public void ModifyDisplayIndex()
	{
		if (GetParentNode() is Category { ItemsA: var itemsA })
		{
			GlobalVarItemIn[] array = (from GlobalVarItemIn p in itemsA.Where((object p) => p is GlobalVarItemIn)
				where p.CustomVisibleA
				select p).ToArray();
			for (int num = 0; num < array.Length; num++)
			{
				array[num].DisplayIndexA = num + 1;
			}
		}
	}

	private void SelectedChangedCmdExecute()
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected Obj, but got Unknown
		if (IsUnConnect())
		{
			return;
		}
		if (DisplayNameA.Equals(AppLocalizationService.GetLang("Lang_GlobalVariable")))
		{
			GlobalVariableSelectDlg selectDlg = new GlobalVariableSelectDlg(true);
			selectDlg.Type = new string[3] { "int", "float", "string" };
			((BaseWindow)(object)selectDlg).ShowDialog();
			if (selectDlg.IsConfirm && !string.IsNullOrEmpty(selectDlg.SelectedGlobalVar))
			{
				VmModule vmModule = VmSolution.Instance.Modules.FirstOrDefault((VmModule x) => x.ID == 13000);
				VmIO vmIO = vmModule.Outputs.FirstOrDefault((VmIO x) => x.Name == selectDlg.SelectedGlobalVar);
				if (vmIO == null)
				{
				}
			}
		}
		if (OldValue != Value)
		{
			if (SetParamUnit() == 0)
			{
				GlobalVarItemList.OldValue = GlobalVarItemList.Value;
				RefreshSendData();
			}
			else
			{
				GlobalVarItemList.Value = GlobalVarItemList.OldValue;
			}
		}
	}

	private void RefreshSendData()
	{
		if (GlobalVarItemList.ReservedProperty != null)
		{
			VmModule vmModule = GlobalVarItemList.ReservedProperty as VmModule;
			if (GlobalVarItemList.CurValueA == 0)
			{
				globalVarType = "";
				GlobalVarItemList.CurrentNameA = AppLocalizationService.GetLang("Lang_PlzChoose");
			}
			else if (GlobalVarItemList.CurValueA > Enumerable.Count(vmModule.Outputs))
			{
				globalVarType = "";
				GlobalVarItemList.CurValue = 0;
				GlobalVarItemList.CurrentNameA = "";
				SetParamUnit();
			}
			else
			{
				VmIO vmIO = vmModule.Outputs.FirstOrDefault((VmIO p) => p.Index == GlobalVarItemList.CurValueA);
				if (vmIO == null && GlobalVarItemList.CurValueA > 0)
				{
					GlobalVarHelper.InitGlobalVarOutputIO(vmModule);
					vmIO = vmModule.Outputs.FirstOrDefault((VmIO p) => p.Index == GlobalVarItemList.CurValueA);
				}
				if (vmIO != null)
				{
					VarItem varItem = new VarItem
					{
						Owner = vmModule,
						Name = vmIO.Name
					};
					globalVarType = vmIO.TypeName;
					GlobalVarItemList.CurrentNameA = varItem.DisplayName;
				}
			}
		}
		if (flagFirstQueue)
		{
			flagFirstQueue = false;
			oldGlobalVarType = globalVarType;
		}
		UpdateOperationParams();
	}

	private void UpdateOperationParams()
	{
		DropOpenSetter.OperationParams = globalVarType;
		if (oldGlobalVarType != globalVarType)
		{
			oldGlobalVarType = globalVarType;
			DelSubscriber();
		}
		else if (!string.IsNullOrWhiteSpace(CurValueA))
		{
			if (string.IsNullOrWhiteSpace(globalVarType))
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
				result = ((string.IsNullOrWhiteSpace(typeName) || !(typeName != globalVarType)) ? true : false);
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
			isThrowException = false;
		}
		catch (Exception)
		{
			isThrowException = true;
		}
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
			text = ((GlobalVarItemList != null) ? (text + "#" + GlobalVarItemList.Value) : (text + "#0"));
			if (!string.IsNullOrWhiteSpace(CommunicationAddress))
			{
				text = text + "#" + CommunicationAddress;
			}
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
		if (GlobalVarItemList == null)
		{
			GlobalVarItemList = new EnumerationVar();
			GlobalVarItemList.Name = "GlobalVarItemList";
			GlobalVarItemList.AccessMode = "O";
			GlobalVarItemList.CurValue = 0;
			GlobalVarItemList.DefaultValue = 0;
			GlobalVarItemList.SetParentNode(this);
		}
		string[] array = value.Split('#');
		if (array.Length < 4)
		{
			return;
		}
		IsAdded = array[2] == "1";
		try
		{
			GlobalVarItemList.CurValue = Convert.ToInt32(array[3]);
			string[] array2 = array[4].Split('+');
			if (array2.Length > 1)
			{
				int result = 0;
				if (int.TryParse(array2[0], out result))
				{
					if (result == 13000)
					{
						GlobalVarItemList.ReservedProperty = VmModuleExtern.GetGlobalVarModule();
					}
					else
					{
						GlobalVarItemList.ReservedProperty = ParamIO.Root.GetLocalVarModule();
					}
				}
			}
		}
		catch (Exception)
		{
			GlobalVarItemList.CurValue = 0;
		}
		int num = -1;
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			BaseNode baseNode = algorithmTabRoot.FindBaseNodeByName("OutputType");
			if (baseNode != null && baseNode is RadioSelecterEx3)
			{
				num = (baseNode as RadioSelecterEx3).CurValueA;
			}
		}
		if (num == 2)
		{
			CustomVisibleA = array[2] == "1";
		}
		else
		{
			CustomVisibleA = false;
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
		if (vmModule != null)
		{
			try
			{
				text = vmModule.Params[Name];
			}
			catch (Exception)
			{
				MyItemState = ItemState.GetParamErro;
				return -1;
			}
		}
		string text2 = text;
		try
		{
			Check(text2);
			if (Value != text2)
			{
				OldValue = text2;
				AnalyseGetMsg(text2);
			}
		}
		catch (Exception ex2)
		{
			string lang = AppLocalizationService.GetLang("Lang_ParamGet");
			HikMessageBox.Show(lang + ex2.Message, MessageIcons.Error);
			LogHelper.Error("获取失败！！！！！名称：" + Name + " des:" + Description + "  获取值:" + text);
		}
		ReInniterBusindess();
		return 0;
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
		int num = 0;
		try
		{
			if (vmModule != null)
			{
				vmModule.Params[Name] = value;
			}
		}
		catch
		{
			num = -1;
		}
		if (num != 0)
		{
			MyItemState = ItemState.SetParamErro;
			OnSetValueCompleted(success: false);
			return num;
		}
		OldValue = value;
		OnSetValueCompleted(success: true);
		SetGlobalIndex();
		return 0;
	}

	private void SetGlobalIndex()
	{
		string text = "";
		Tab tab = GetParentNode<Tab>();
		if (tab != null && tab.Collection != null)
		{
			foreach (NameValueItem item in tab.Collection)
			{
				if (item is GlobalVarItemIn && item.CustomVisibleA)
				{
					text = text + (item as GlobalVarItemIn).Value + "\r";
				}
			}
		}
		if (!string.IsNullOrEmpty(text))
		{
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
				vmModule.UserData["GlobalVarRecord"] = StringUtil.UTF8GetBytesPadZero(text);
			}
		}
	}

	public override string ToString()
	{
		return Name + "_IsAdd[" + IsAddedA + "]_CustomVisible[" + CustomVisibleA + "]";
	}

	private void TmpEntry_OnItemClicked(object param)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected Obj, but got Unknown
		if (param == null)
		{
			return;
		}
		string text = param.ToString();
		if (IsUnConnect())
		{
			return;
		}
		if (text.Equals(AppLocalizationService.GetLang("Lang_VariableManager")))
		{
			GlobalVariableSelectDlg val = new GlobalVariableSelectDlg(true);
			val.Type = new string[3] { "int", "float", "string" };
			IVmModule localVarModule = ParamIO.Root.GetLocalVarModule();
			val.DecorateSource = localVarModule;
			((BaseWindow)(object)val).ShowDialog();
			if (val.IsConfirm && val.SelectedVarItem is VarItem varItem)
			{
				GlobalVarItemList.Value = varItem.Index.ToString();
				GlobalVarItemList.CurrentNameA = varItem.DisplayName;
				GlobalVarItemList.ReservedProperty = varItem.Owner;
			}
		}
		else
		{
			GlobalVarItemList.Value = "0";
			GlobalVarItemList.ReservedProperty = null;
			GlobalVarItemListA.CurrentNameA = AppLocalizationService.GetLang("Lang_PlzChoose");
		}
		if (OldValue != Value)
		{
			if (SetParamUnit() == 0)
			{
				GlobalVarItemList.OldValue = GlobalVarItemList.Value;
				RefreshSendData();
			}
			else
			{
				GlobalVarItemList.Value = GlobalVarItemList.OldValue;
			}
		}
	}

	public override void InniterBusiness()
	{
	}

	private void ReInniterBusindess()
	{
		if (Initers != null)
		{
			foreach (Setter initer in Initers)
			{
				initer.Operate(null);
			}
		}
		if (ExternIniters == null)
		{
			return;
		}
		foreach (ITrigger externIniter in ExternIniters)
		{
			externIniter.OnTrigger(this);
		}
	}
}
