using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Apps.Localization;
using Hik.Util;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class BasicItemsSend : NameValueItem
{
	private const string SEND_DATA_1 = "SendData1";

	private const string SEND_DATA_2 = "SendData2";

	private const string SEND_DATA_3 = "SendData3";

	private const string SEND_DATA_4 = "SendData4";

	private const string SEND_DATA_5 = "SendData5";

	private const string SEND_DATA_6 = "SendData6";

	private const string SEND_DATA_7 = "SendData7";

	private const string SEND_DATA_8 = "SendData8";

	private const string SEND_DATA_9 = "SendData9";

	private const string SEND_DATA_10 = "SendData10";

	private const string SEND_DATA_11 = "SendData11";

	private const string SEND_DATA_12 = "SendData12";

	private const string SEND_DATA_13 = "SendData13";

	private const string SEND_DATA_14 = "SendData14";

	private const string SEND_DATA_15 = "SendData15";

	private const string SEND_DATA_16 = "SendData16";

	private EnumerationCommunication commDevice;

	public ObservableCollection<BasicItemSend> Items = null;

	private DelegateCommand addCommand = null;

	public int MaxCount => 16;

	public int MinCount => 1;

	public EnumerationCommunication CommDevice
	{
		get
		{
			if (commDevice == null)
			{
				Tab tab = GetParentNode<Tab>();
				if (tab != null)
				{
					commDevice = tab.FindBaseNodeByName("CommunicationModuleID") as EnumerationCommunication;
				}
			}
			return commDevice;
		}
	}

	public ObservableCollection<BasicItemSend> ItemsA
	{
		get
		{
			if (Items == null)
			{
				Items = new ObservableCollection<BasicItemSend>
				{
					new BasicItemSend("BasicItem1", "SendData1", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem2", "SendData2", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem3", "SendData3", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem4", "SendData4", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem5", "SendData5", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem6", "SendData6", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem7", "SendData7", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem8", "SendData8", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem9", "SendData9", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem10", "SendData10", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem11", "SendData11", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem12", "SendData12", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem13", "SendData13", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem14", "SendData14", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem15", "SendData15", isAdd: false, canDelete: false, this),
					new BasicItemSend("BasicItem16", "SendData16", isAdd: false, canDelete: false, this)
				};
			}
			return Items;
		}
	}

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
				if (!value)
				{
					HideEveryItem();
				}
				else if (value)
				{
					GetCurItems();
				}
				CustomVisible = value;
				OnPropertyChanged("CustomVisibleA");
			}
		}
	}

	public ICommand AddCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (addCommand == null)
			{
				addCommand = new DelegateCommand((Action)AddCommandExecute);
			}
			return (ICommand)addCommand;
		}
	}

	private void AddBasicItem()
	{
		if (Items != null)
		{
			BasicItemSend basicItemSend = Items.FirstOrDefault((BasicItemSend p) => !p.CustomVisibleA && !p.IsAddedA);
			if (basicItemSend != null)
			{
				basicItemSend.IsAddedA = true;
				basicItemSend.CustomVisibleA = true;
				basicItemSend.ModifyDisplayIndex();
				SetVisibleUserData();
				GetVmModule()?.OnPropertyChanged("IsReady");
			}
		}
	}

	public void SetVisibleUserData()
	{
		if (ItemsA == null)
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (BasicItemSend item in ItemsA)
		{
			stringBuilder.Append($"{item.NameA}-{item.CustomVisibleA}-{item.IsAddedA};");
		}
		VmModule vmModule = GetVmModule();
		if (vmModule != null)
		{
			vmModule.UserData[$"params.{NameA}"] = StringUtil.UTF8GetBytesPadZero(stringBuilder.ToString());
		}
	}

	private void GetVisibleUserData()
	{
		VmModule vmModule = GetVmModule();
		if (vmModule == null)
		{
			return;
		}
		byte[] array = vmModule.UserData[$"params.{NameA}"];
		if (array == null || array.Length == 0)
		{
			return;
		}
		string text = StringUtil.UTF8GetString(array);
		string[] array2 = text.Split(';');
		for (int i = 0; i < array2.Length; i++)
		{
			string[] tmpStr = array2[i].Split('-');
			BasicItemSend basicItemSend = ItemsA?.FirstOrDefault((BasicItemSend x) => x.NameA == tmpStr[0]);
			if (basicItemSend != null)
			{
				basicItemSend.CustomVisible = bool.Parse(tmpStr[1]);
				basicItemSend.IsAdded = bool.Parse(tmpStr[2]);
				basicItemSend.DisplayIndexA = (i + 1).ToString();
				if (i == array2.Length - 2)
				{
					basicItemSend.ModifyDeleteBtnVisibility();
				}
			}
		}
	}

	public void GetCurItems()
	{
		BasicItemBase[] array = (from BasicItemBase p in Items.Where((BasicItemSend p) => p != null)
			where p.IsAddedA
			select p).ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			array[num].CustomVisibleA = true;
		}
		if (array.Length == 0)
		{
			AddBasicItem();
		}
	}

	public int GetCurCount()
	{
		if (Items == null)
		{
			return 0;
		}
		return Enumerable.Count(Items, (BasicItemSend p) => p.IsAddedA);
	}

	private void HideEveryItem()
	{
		if (Items == null)
		{
			return;
		}
		foreach (BasicItemSend item in Items)
		{
			item.CustomVisibleA = false;
		}
	}

	private void AddCommandExecute()
	{
		int curCount = GetCurCount();
		if (curCount >= MaxCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + ":" + MaxCount, MessageButtons.Normal, MessageIcons.Warning);
		}
		else
		{
			AddBasicItem();
		}
	}

	public override int GetParamUnit()
	{
		if (ItemsA != null)
		{
			foreach (BasicItemSend item in ItemsA)
			{
				if (item.CustomVisibleA)
				{
					item.GetParamUnit();
				}
			}
		}
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		if (ItemsA != null)
		{
			GetVisibleUserData();
			BasicItemBase[] array = (from BasicItemBase p in Items.Where((BasicItemSend p) => p != null)
				where p.IsAddedA
				select p).ToArray();
			BasicItemBase[] array2 = array;
			foreach (BasicItemBase basicItemBase in array2)
			{
				basicItemBase.InniterBusiness();
			}
		}
	}

	public void ResetItems(int index)
	{
		for (int i = index - 1; i <= GetCurCount() - 2; i++)
		{
			ItemsA[i].IsAddedA = ItemsA[i + 1].IsAddedA;
			ItemsA[i].DropOpenSetterA.Value = ItemsA[i + 1].DropOpenSetterA.Value;
			ItemsA[i].CurValueA = ItemsA[i + 1].CurValueA;
		}
		ItemsA[GetCurCount() - 1].CustomVisibleA = false;
		ItemsA[GetCurCount() - 1].CurValueA = null;
		ItemsA[GetCurCount() - 1].IsAddedA = false;
	}
}
