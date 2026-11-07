using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Apps.Interface;
using Apps.Log;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class IfModuleDynamicIO : DynamicIOContainer<IfDynamicIO>
{
	private int _selectTypeIndex = 0;

	private bool _isAllowSet = true;

	private int _decimalDigits = 0;

	public ObservableCollection<object> EnumEntrys = null;

	private DynamicIOContainer<IfDynamicIO> outputio;

	public int SelectTypeIndex
	{
		get
		{
			return _selectTypeIndex;
		}
		set
		{
			_selectTypeIndex = value;
			OnPropertyChanged("SelectTypeIndex");
		}
	}

	public bool IsAllowSet
	{
		get
		{
			return _isAllowSet;
		}
		set
		{
			if (_isAllowSet != value)
			{
				_isAllowSet = value;
				OnPropertyChanged("IsAllowSet");
			}
		}
	}

	public int DecimalDigits
	{
		get
		{
			return _decimalDigits;
		}
		set
		{
			if (_decimalDigits != value)
			{
				_decimalDigits = value;
				OnPropertyChanged("DecimalDigits");
			}
		}
	}

	public new ObservableCollection<object> EnumEntrysA
	{
		get
		{
			if (EnumEntrys == null)
			{
				EnumEntrys = new ObservableCollection<object>
				{
					new EnumEntry
					{
						Description = "int",
						DisplayName = "int",
						StrValue = "int"
					},
					new EnumEntry
					{
						Description = "float",
						DisplayName = "float",
						StrValue = "float"
					}
				};
			}
			return EnumEntrys;
		}
	}

	public DynamicIOContainer<IfDynamicIO> Outputio
	{
		get
		{
			if (outputio == null)
			{
				outputio = new DynamicIOContainer<IfDynamicIO>
				{
					IsOutput = true,
					DataSource = (Owner as VmModule)
				};
			}
			return outputio;
		}
		set
		{
			outputio = value;
		}
	}

	public override string DefaultTypeName => (SelectTypeIndex == 0) ? "int" : "float";

	public override string DefaultName => (SelectTypeIndex == 0) ? "int" : "float";

	public IfModuleDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	public override object AddIO(string name = null, string type = null, int index = -1)
	{
		type = ((SelectTypeIndex == 0) ? "int" : "float");
		object obj = base.AddIO(name, type, index);
		if (obj == null)
		{
			return obj;
		}
		if (obj is IfDynamicIO ifDynamicIO)
		{
			ifDynamicIO.SelectType = type;
			Outputio.AddIOWithoutVerification(ifDynamicIO.DisplayNameA + "Out", "int", index);
			Outputio.AddIOWithoutVerification(ifDynamicIO.DisplayNameA + "Min", type, index);
			Outputio.AddIOWithoutVerification(ifDynamicIO.DisplayNameA + "Max", type, index);
		}
		return obj;
	}

	public override void RemoveIO(object dymaicIO)
	{
		base.RemoveIO(dymaicIO);
		IfDynamicIO ifDynamicIO;
		if ((ifDynamicIO = dymaicIO as IfDynamicIO) == null)
		{
			return;
		}
		foreach (Integer item in ifDynamicIO.IntegerBettween.IntegersA)
		{
			item.IsSubA = false;
			item.SubData.CurValueA = null;
		}
		foreach (Float item2 in ifDynamicIO.Floatbetween.FloatsA)
		{
			item2.IsSubA = false;
			item2.SubData.CurValueA = null;
		}
		if (string.IsNullOrEmpty(ifDynamicIO.DisplayNameA))
		{
			return;
		}
		ObservableCollection<object> itemsA = Outputio.ItemsA;
		if (itemsA == null || itemsA.Count <= 0)
		{
			return;
		}
		IEnumerable<IfDynamicIO> enumerable = from IfDynamicIO item in Outputio.ItemsA
			where !string.IsNullOrEmpty(item.DisplayNameA)
			select item;
		if (enumerable == null || enumerable.Count() <= 0)
		{
			return;
		}
		List<IfDynamicIO> list = enumerable.Where((IfDynamicIO item) => item.DisplayNameA.StartsWith(ifDynamicIO.DisplayNameA))?.ToList();
		if (list != null && list.Count > 0)
		{
			for (int num = 0; num < Enumerable.Count(list); num++)
			{
				Outputio.RemoveIO(list[num]);
			}
		}
	}

	public void InitSource(int decimaldigit)
	{
		DecimalDigits = decimaldigit;
		GetParam();
	}

	public void GetParam()
	{
		IsAllowSet = false;
		if (Owner == null)
		{
			return;
		}
		string text = "";
		byte[] array = null;
		try
		{
			array = ((IUserBytesData)(Owner as VmModule).Params)["IFText"];
		}
		catch (Exception ex)
		{
			LogHelper.Error("获取IFText失败：" + ex.Message);
			IsAllowSet = true;
			return;
		}
		text = StringUtil.UTF8GetString(array);
		string[] array2 = text.Split(';');
		if (array2.Length >= 1)
		{
			foreach (IfDynamicIO item in ItemsA)
			{
				if (!item.CustomVisibleA)
				{
					continue;
				}
				try
				{
					string[] array3 = array2;
					foreach (string text2 in array3)
					{
						string[] array4 = text2.Split(':');
						if (array4.Length > 2 && array4[0] == item.NameA)
						{
							item.SelectType = item.FilterTypeA;
							if (item.CurrentValueBettweenValue[0] is Float)
							{
								(item.CurrentValueBettweenValue[0] as Float).Value = array4[1];
								(item.CurrentValueBettweenValue[1] as Float).Value = array4[2];
							}
							else if (item.CurrentValueBettweenValue[0] is Integer)
							{
								(item.CurrentValueBettweenValue[0] as Integer).Value = array4[1];
								(item.CurrentValueBettweenValue[1] as Integer).Value = array4[2];
							}
							item.RemarkInfo = array4[3];
						}
					}
				}
				catch (Exception)
				{
				}
			}
		}
		IsAllowSet = true;
	}
}
