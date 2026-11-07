using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ProcedureCommDynamicIO(string name, IOwner dataSorce) : DynamicIOContainer<ProcedureCommItemDynamicIO>(name, dataSorce)
{
	private string lastCheckedObj;

	private VmModule procedureComm;

	private ObservableCollection<ProcedureCommUnitDynamicIO> _ObservableCollectionA = new ObservableCollection<ProcedureCommUnitDynamicIO>();

	private int _MaxCount = 8;

	private bool _IsRegularChecked = true;

	private bool _IsEndChecked = false;

	private DelegateCommand<object> _IsCheckedCommand = null;

	private DelegateCommand _AddCommand = null;

	public VmModule ProcedureComm
	{
		get
		{
			if (procedureComm == null)
			{
				procedureComm = ((parentNode as IOwner).Owner as VmModuleSet).ProcedureCommunicaitonModule as VmModule;
			}
			return procedureComm;
		}
	}

	public ObservableCollection<ProcedureCommUnitDynamicIO> ObservableCollectionA
	{
		get
		{
			return _ObservableCollectionA;
		}
		set
		{
			_ObservableCollectionA = value;
		}
	}

	public int MaxCount => _MaxCount;

	public bool IsRegularChecked
	{
		get
		{
			return _IsRegularChecked;
		}
		set
		{
		}
	}

	public bool IsEndChecked
	{
		get
		{
			return _IsEndChecked;
		}
		set
		{
		}
	}

	public ICommand IsCheckedCommand => (ICommand)(_IsCheckedCommand ?? (_IsCheckedCommand = new DelegateCommand<object>((Action<object>)IsCheckedCommandExecute)));

	public new ICommand AddCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = _AddCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)AddIOCommandExecute);
				DelegateCommand val3 = val2;
				_AddCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	internal void InitSource()
	{
		try
		{
			if (ProcedureComm == null)
			{
				return;
			}
			string[] array = ProcedureComm.Params["AllItemsId"].TrimEnd(';').Split(';');
			if (array.Length == 0 || string.IsNullOrEmpty(array[0]))
			{
				return;
			}
			ObservableCollectionA = new ObservableCollection<ProcedureCommUnitDynamicIO>();
			string[] array2 = array;
			foreach (string id in array2)
			{
				ProcedureCommUnitDynamicIO procedureCommUnitDynamicIO = new ProcedureCommUnitDynamicIO();
				procedureCommUnitDynamicIO.DataSource = ProcedureComm;
				procedureCommUnitDynamicIO.SetParentNode(this);
				ProcedureComm.Params["SelectItem"] = id;
				string text = ProcedureComm.Params["ItemMode"];
				_IsRegularChecked = text == "1";
				OnPropertyChanged("IsRegularChecked");
				_IsEndChecked = text == "2";
				OnPropertyChanged("IsEndChecked");
				ProcedureComm.Params["SelectItem"] = id;
				procedureCommUnitDynamicIO.ID = int.Parse(id);
				string text2 = ProcedureComm.Params["TargetDevice"];
				string[] array3 = text2.TrimEnd('#').Split('#');
				if (array3.Count() == 3)
				{
					procedureCommUnitDynamicIO.CommDeviceSelecterA = new EnumerationAllCom
					{
						CurValue = int.Parse(array3[1]),
						SelectedChangedCommand = procedureCommUnitDynamicIO.SelectedChangedCommand_CommDevice
					};
					if (array3[2] == "PLC")
					{
						int.TryParse(procedureComm.Params["AddressIndex"], out var result);
						procedureCommUnitDynamicIO.AddressItemList = new Enumeration
						{
							CurValueA = ((result != -1) ? result : 0)
						};
						procedureCommUnitDynamicIO.PLCVisibleA = true;
					}
					procedureCommUnitDynamicIO.FilterSelecterIfJudge = (procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%condition_" + id + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
					procedureCommUnitDynamicIO.FilterSelecterOKInput = (procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%ok_send_" + id + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
					procedureCommUnitDynamicIO.FilterSelecterNGInput = (procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%ng_send_" + id + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
				}
				else if (array3.Count() == 5)
				{
					procedureCommUnitDynamicIO.IOVisibleA = true;
					if (int.TryParse(array3[1], out var indexDevice) && procedureCommUnitDynamicIO.CommDeviceSelecterA.EnumEntrysA.FirstOrDefault((object x) => (x as EnumEntry).Value == indexDevice) is EnumEntry enumEntry)
					{
						procedureCommUnitDynamicIO.oldIOControl = enumEntry.FullName;
						procedureCommUnitDynamicIO.CommDeviceSelecterA.CurValue = indexDevice;
						object reservedProperty = enumEntry.ReservedProperty;
						if (reservedProperty == null)
						{
							LogHelper.Error("Selected Device is error");
							return;
						}
						ObservableCollection<object> observableCollection = new ObservableCollection<object>();
						observableCollection.Add(new EnumEntry
						{
							Name = AppLocalizationService.GetLang("Lang_PlzChoose"),
							DisplayName = "%" + AppLocalizationService.GetLang("Lang_PlzChoose") + "%",
							Value = 0,
							Description = "%" + AppLocalizationService.GetLang("Lang_PlzChoose") + "%",
							Tag = enumEntry
						});
						for (int num = 0; num < enumEntry.IoNum; num++)
						{
							observableCollection.Add(new EnumEntry
							{
								Name = "IO",
								DisplayName = $"%IO {num + 1}%",
								Value = num + 1,
								Description = $"%IO {num + 1}%",
								Tag = enumEntry
							});
						}
						procedureCommUnitDynamicIO.FilterSelecterIfJudge = (procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%condition_" + id + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
						procedureCommUnitDynamicIO.IOItemOKSendListA.EnumEntrysA = observableCollection;
						procedureCommUnitDynamicIO.IOItemNGSendListA.EnumEntrysA = observableCollection;
						procedureCommUnitDynamicIO.IOItemTimeoutSendListA.EnumEntrysA = observableCollection;
						if (int.TryParse(ProcedureComm.Params["ControllerOK"], out var result2))
						{
							procedureCommUnitDynamicIO.IOItemOKSendListA.CurValue = ((result2 != -1) ? result2 : 0);
						}
						if (int.TryParse(ProcedureComm.Params["ControllerNG"], out var result3))
						{
							procedureCommUnitDynamicIO.IOItemNGSendListA.CurValue = ((result3 != -1) ? result3 : 0);
						}
						if (int.TryParse(ProcedureComm.Params["ControllerTimeout"], out var result4))
						{
							procedureCommUnitDynamicIO.IOItemTimeoutSendListA.CurValue = ((result4 != -1) ? result4 : 0);
						}
					}
				}
				else
				{
					LogHelper.Error("TargetDevice error" + text2);
					procedureCommUnitDynamicIO.FilterSelecterIfJudge = (procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%condition_" + id + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
					procedureCommUnitDynamicIO.FilterSelecterOKInput = (procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%ok_send_" + id + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
					procedureCommUnitDynamicIO.FilterSelecterNGInput = (procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%ng_send_" + id + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
				}
				if (IsRegularChecked)
				{
					int result5 = 100;
					int.TryParse(ProcedureComm.Params["Interval"], out result5);
					procedureCommUnitDynamicIO.SetInterval.CurValueA = result5;
					procedureCommUnitDynamicIO.FilterSelecterTimeoutSend = (procedureCommUnitDynamicIO.ItemsA.FirstOrDefault((object p) => (p as ProcedureCommItemDynamicIO).NameA == "%timeout_send_" + id + "%") as ProcedureCommItemDynamicIO)?.FilterSelecter;
				}
				ObservableCollectionA.Add(procedureCommUnitDynamicIO);
			}
			ModifyDisplayIndex();
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message + Environment.NewLine + ex.StackTrace);
		}
	}

	private void IsCheckedCommandExecute(object obj)
	{
		string text = obj.ToString();
		if (!(lastCheckedObj != text))
		{
			return;
		}
		if (ObservableCollectionA.Count > 0)
		{
			if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SwitchProcedureCommMode"), MessageIcons.Question) != DialogResult.Ok)
			{
				return;
			}
			lastCheckedObj = text;
			if (text == "IsEnd")
			{
				_IsEndChecked = true;
				OnPropertyChanged("IsEndChecked");
				_IsRegularChecked = false;
				OnPropertyChanged("IsRegularChecked");
			}
			else if (text == "IsRegular")
			{
				_IsEndChecked = false;
				OnPropertyChanged("IsEndChecked");
				_IsRegularChecked = true;
				OnPropertyChanged("IsRegularChecked");
			}
			foreach (ProcedureCommUnitDynamicIO item in ObservableCollectionA)
			{
				ObservableCollection<object> observableCollection = new ObservableCollection<object>();
				foreach (object item2 in item.ItemsA)
				{
					observableCollection.Add(item2);
				}
				foreach (object item3 in observableCollection)
				{
					item.RemoveIO(item3);
				}
			}
			if (ProcedureComm != null)
			{
				ProcedureComm.Params["DeleteAllItems"] = "";
			}
			ObservableCollectionA.Clear();
		}
		else
		{
			lastCheckedObj = text;
			if (text == "IsEnd")
			{
				_IsEndChecked = true;
				OnPropertyChanged("IsEndChecked");
				_IsRegularChecked = false;
				OnPropertyChanged("IsRegularChecked");
			}
			else if (text == "IsRegular")
			{
				_IsEndChecked = false;
				OnPropertyChanged("IsEndChecked");
				_IsRegularChecked = true;
				OnPropertyChanged("IsRegularChecked");
			}
		}
	}

	public void AddIOCommandExecute()
	{
		try
		{
			int num = Enumerable.Count(ObservableCollectionA);
			if (num >= MaxCount)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + ":" + MaxCount, MessageButtons.Normal, MessageIcons.Warning);
				return;
			}
			ProcedureCommUnitDynamicIO procedureCommUnitDynamicIO = new ProcedureCommUnitDynamicIO();
			procedureCommUnitDynamicIO.DataSource = ProcedureComm;
			procedureCommUnitDynamicIO.IsOutput = false;
			procedureCommUnitDynamicIO.SetParentNode(this);
			object obj = procedureCommUnitDynamicIO.AddIO($"condition_{procedureCommUnitDynamicIO.ID}", "string");
			if (obj == null)
			{
				return;
			}
			procedureCommUnitDynamicIO.FilterSelecterIfJudge = (obj as ProcedureCommItemDynamicIO)?.FilterSelecter;
			obj = procedureCommUnitDynamicIO.AddIO($"ok_send_{procedureCommUnitDynamicIO.ID}", "string");
			if (obj == null)
			{
				return;
			}
			procedureCommUnitDynamicIO.FilterSelecterOKInput = (obj as ProcedureCommItemDynamicIO)?.FilterSelecter;
			obj = procedureCommUnitDynamicIO.AddIO($"ng_send_{procedureCommUnitDynamicIO.ID}", "string");
			if (obj == null)
			{
				return;
			}
			procedureCommUnitDynamicIO.FilterSelecterNGInput = (obj as ProcedureCommItemDynamicIO)?.FilterSelecter;
			if (ProcedureComm == null)
			{
				return;
			}
			ProcedureComm.Params["AddItem"] = procedureCommUnitDynamicIO.ID.ToString();
			ProcedureComm.Params["ItemMode"] = (IsRegularChecked ? "1" : "2");
			if (IsRegularChecked)
			{
				obj = procedureCommUnitDynamicIO.AddIO($"timeout_send_{procedureCommUnitDynamicIO.ID}", "string");
				if (obj == null)
				{
					return;
				}
				procedureCommUnitDynamicIO.FilterSelecterTimeoutSend = (obj as ProcedureCommItemDynamicIO)?.FilterSelecter;
				ProcedureComm.Params["SelectItem"] = procedureCommUnitDynamicIO.ID.ToString();
				ProcedureComm.Params["Interval"] = 100.ToString();
			}
			ObservableCollectionA.Add(procedureCommUnitDynamicIO);
			ModifyDisplayIndex();
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message + Environment.NewLine + ex.StackTrace);
		}
	}

	internal void ModifyDisplayIndex()
	{
		for (int i = 0; i < ObservableCollectionA.Count; i++)
		{
			if (ObservableCollectionA[i] == null)
			{
				continue;
			}
			ProcedureCommUnitDynamicIO procedureCommUnitDynamicIO = ObservableCollectionA[i];
			if (procedureCommUnitDynamicIO != null)
			{
				procedureCommUnitDynamicIO.DisplayIndex = (i + 1).ToString();
				if (Enumerable.Count(ObservableCollectionA) == 1)
				{
					procedureCommUnitDynamicIO.DelBtnVisible = false;
				}
				else
				{
					procedureCommUnitDynamicIO.DelBtnVisible = true;
				}
			}
		}
	}
}
