using System;
using System.Collections.Generic;
using System.Text;
using HikExternalCall.Common;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class DataAnalysisDynamicIO : DynamicIO
{
	public int _orderType = 1;

	private IntegerBettween _byteStartEndValue;

	public override List<string> IoSelectValueTypes => new List<string> { "int", "float", "string" };

	public List<EnumEntry> OrderTypes => new List<EnumEntry>
	{
		new EnumEntry
		{
			DisplayNameA = "ABCD",
			ValueA = 1
		},
		new EnumEntry
		{
			DisplayNameA = "BADC",
			ValueA = 2
		},
		new EnumEntry
		{
			DisplayNameA = "DCBA",
			ValueA = 3
		},
		new EnumEntry
		{
			DisplayNameA = "CDAB",
			ValueA = 4
		}
	};

	public int OrderTypeA
	{
		get
		{
			return _orderType;
		}
		set
		{
			if (value != _orderType)
			{
				_orderType = value;
				SetParamUnit();
				OnPropertyChanged("OrderTypeA");
			}
		}
	}

	public IntegerBettween ByteStartEndValue
	{
		get
		{
			if (_byteStartEndValue == null)
			{
				_byteStartEndValue = new IntegerBettween
				{
					DisplayNameA = "Lang_StringLength",
					DescriptionA = "Lang_StringLength",
					MinValueA = 0,
					MaxValueA = 999999999,
					IncValueA = 1
				};
				_byteStartEndValue.Integers = new List<object>();
				Integer integer = new Integer
				{
					MinValueA = 0,
					MaxValueA = 999999999,
					CurValueA = 0,
					IncValueA = 1
				};
				integer.OnCurValueChanged += Params_OnCurValueChanged;
				_byteStartEndValue.IntegersA.Add(integer);
				Integer integer2 = new Integer
				{
					MinValueA = 0,
					MaxValueA = 999999999,
					CurValueA = 1,
					IncValueA = 1
				};
				integer2.OnCurValueChanged += Params_OnCurValueChanged;
				_byteStartEndValue.IntegersA.Add(integer2);
			}
			return _byteStartEndValue;
		}
	}

	public int ByteLengthA => 30;

	public override void TextChangedExecute(string newName)
	{
		if (GetSelectedType() == 2 && Encoding.UTF8.GetByteCount(newName) > 30)
		{
			OnPropertyChanged("NameA");
		}
		else
		{
			base.TextChangedExecute(newName);
		}
	}

	private int GetSelectedType()
	{
		int result = -1;
		object algorithmItems = (GetParentNode() as DataAnalysisModuleDynamicIO).AlgorithmItems;
		if (algorithmItems != null && (algorithmItems as AlgorithmTabRoot).FindBaseNodeByName("AnalysisType") is RadioSelecterAnaly radioSelecterAnaly)
		{
			result = radioSelecterAnaly.CurValueA;
		}
		return result;
	}

	public void Params_OnCurValueChanged(object sender, EventArgs e)
	{
		SetParamUnit();
	}

	public override int SetParamUnit()
	{
		int result = 0;
		if (GetSelectedType() == 0)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (DynamicIO item in (GetParentNode() as DynamicIOContainer<DataAnalysisDynamicIO>).ItemsA)
			{
				if (item.CustomVisibleA)
				{
					stringBuilder.Append(item.NameA);
					stringBuilder.Append("\n");
					stringBuilder.Append(item.ValueTypeA);
					stringBuilder.Append("\r");
				}
			}
			if (string.IsNullOrEmpty(stringBuilder.ToString()))
			{
				return -1;
			}
			IoDataSource.Root.Params["OutputField"] = stringBuilder.ToString();
		}
		else if (GetSelectedType() == 2)
		{
			List<Analy_BYTES_RULE> list = new List<Analy_BYTES_RULE>();
			foreach (DataAnalysisDynamicIO item2 in (GetParentNode() as DynamicIOContainer<DataAnalysisDynamicIO>).ItemsA)
			{
				if (item2.CustomVisibleA)
				{
					list.Add(new Analy_BYTES_RULE
					{
						chName = StringUtil.UTF8GetFixLenBytes(item2.NameA, 32),
						eParamType = (PARAM_VALUE_TYPE)Enum.Parse(typeof(PARAM_VALUE_TYPE), (item2.ValueTypeA == "int") ? "0" : ((item2.ValueTypeA == "float") ? "1" : "2")),
						eOrderType = (GW_ORDER_TYPE)Enum.Parse(typeof(GW_ORDER_TYPE), item2.OrderTypeA.ToString()),
						nStartIndex = (item2.ByteStartEndValue.Integers[0] as Integer).CurValueA,
						nEndIndex = (item2.ByteStartEndValue.Integers[1] as Integer).CurValueA
					});
				}
			}
			if (list.Count > 0)
			{
				((IUserBytesData)IoDataSource.Root.Params)["BytesData"] = ExternalCallHelper.StructsToBytes(list);
			}
			else
			{
				((IUserBytesData)IoDataSource.Root.Params)["BytesData"] = new byte[1] { 48 };
			}
		}
		return result;
	}
}
