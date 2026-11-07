using System;
using System.Collections.Generic;
using System.Linq;
using Apps.Interface;
using Hik.Xml;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser;

public abstract class FilterBase : ParamItem, IAccessMode, ISelectable, IGetResultValue, IGetDataPath
{
	public const int TYPE_GLOBALVAR = 2;

	public const int TYPE_QUEUE = 0;

	public const int TYPE_COMMUNICATION = 1;

	public const string SPILT_COLON = ":";

	[XmlMark(true)]
	public string StructName;

	[XmlMark(true)]
	public string ValueType;

	[XmlMark(true)]
	public bool IsForce;

	[XmlMark(true)]
	public bool IsShow;

	[XmlMark(true)]
	public string AccessMode = "RW";

	[XmlMark(true)]
	public bool Visible = true;

	[XmlMark(true)]
	public bool CustomVisible = true;

	[XmlMark(true)]
	public int QueueIndex = 0;

	[XmlMark(true)]
	public int GlobalIndex = 0;

	[XmlMark(true)]
	public int CommDeviceIndex = 0;

	[XmlMark(true)]
	public bool CanSubscribe = true;

	[XmlMark(true)]
	public bool CanDelete = false;

	[XmlMark(true)]
	public bool AutoSubscribe = true;

	[XmlMark(true)]
	public string filterID;

	[XmlMark(true)]
	public bool ForceAutoSubscribe = false;

	[XmlMark(true)]
	public bool AddUpInLoop = true;

	protected uint executeCount;

	public string _relativeIndexStringName;

	public bool GetValueReady = false;

	private object _valueEx;

	private object _value;

	public VmModule Owner { get; set; }

	public bool isConstantValue => Owner == null;

	public string StructNameA
	{
		get
		{
			return StructName;
		}
		set
		{
			StructName = value;
			OnPropertyChanged("StructNameA");
		}
	}

	public bool IsShowA
	{
		get
		{
			return IsShow;
		}
		set
		{
			IsShow = value;
			OnPropertyChanged("IsShowA");
		}
	}

	public string AccessModeA
	{
		get
		{
			return AccessMode;
		}
		set
		{
			AccessMode = value;
			OnPropertyChanged("AccessModeA");
		}
	}

	public virtual bool CustomVisibleA
	{
		get
		{
			return CustomVisible;
		}
		set
		{
			if (CustomVisible != value)
			{
				CustomVisible = value;
				OnPropertyChanged("CustomVisibleA");
			}
		}
	}

	public virtual string ValueTypeA
	{
		get
		{
			return ValueType;
		}
		set
		{
			ValueType = value;
			OnPropertyChanged("ValueTypeA");
		}
	}

	public int ValueTypeIndex
	{
		get
		{
			switch (ValueType)
			{
			case "int":
			case "pixelformat":
				return 1;
			case "float":
				return 2;
			case "string":
				return 3;
			case "image":
				return 4;
			case "pointset":
			case "chunk":
				return 5;
			case "bytes":
				return 6;
			case "double":
				return 7;
			default:
				return 0;
			}
		}
	}

	public bool IsValueExRegister => ValueExChanged != null;

	public string FilterName => Name;

	public int LengthA => 31;

	public string Type => ValueType;

	public string accessMode
	{
		get
		{
			return AccessMode;
		}
		set
		{
			AccessMode = value;
		}
	}

	public bool IsForceA
	{
		get
		{
			return IsForce;
		}
		set
		{
			IsForce = value;
			OnPropertyChanged("IsForce");
		}
	}

	public string RelativeIndexStringName
	{
		get
		{
			return _relativeIndexStringName;
		}
		set
		{
			_relativeIndexStringName = value;
			OnPropertyChanged("RelativeIndexStringName");
		}
	}

	public override string ParamValueType => ValueType;

	public virtual object ValueEx
	{
		get
		{
			return _valueEx;
		}
		set
		{
			GetValueReady = true;
			if (_valueEx != value)
			{
				_valueEx = value;
				OnPropertyChanged("ValueEx");
			}
		}
	}

	public bool IsValueRegister => ValueChanged != null;

	public object Value
	{
		get
		{
			return _value;
		}
		set
		{
			if (_value != value)
			{
				_value = value;
				if (IsValueRegister)
				{
					ValueChanged(this, new EventArgs());
				}
			}
		}
	}

	public event EventHandler ValueExChanged;

	public event EventHandler ValueChanged;

	public void SetValueFromServer(ImvsSdkDefine.IMVS_MODULE_RESULT_INFO_EX_Data struResultInfo, uint curExecuteCount)
	{
		if (curExecuteCount != executeCount)
		{
			ValueEx = null;
			executeCount = curExecuteCount;
		}
		switch (struResultInfo.nParamType)
		{
		case 1:
			SetValue(Array.ConvertAll(struResultInfo.pIntValue, (Converter<int, int?>)((int s) => s)));
			break;
		case 2:
			SetValue(Array.ConvertAll(struResultInfo.pFloatValue, (Converter<float, float?>)((float s) => s)));
			break;
		case 3:
			SetValue(struResultInfo.pStringValue);
			break;
		case 4:
			SetValue(struResultInfo.pImageValue);
			break;
		case 5:
			SetValue(struResultInfo.pChunkValue);
			break;
		case 6:
			SetValue(struResultInfo.pByteValue);
			break;
		case 7:
			SetValue(struResultInfo.pDoubleValue);
			break;
		}
	}

	public void SetDefaultValue(uint curExecuteCount)
	{
		if (curExecuteCount != executeCount)
		{
			ValueEx = null;
			executeCount = curExecuteCount;
		}
		switch (ValueTypeIndex)
		{
		case 1:
			SetValue(new int?[1]);
			break;
		case 2:
			SetValue(new float?[1]);
			break;
		case 3:
			SetValue(new ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX[1]);
			break;
		case 7:
			SetValue(new double?[1]);
			break;
		case 4:
		case 5:
		case 6:
			break;
		}
	}

	private void SetValue<T>(IEnumerable<T> newValue)
	{
		if (ValueEx == null)
		{
			ValueEx = newValue;
		}
		else if (AddUpInLoop)
		{
			IEnumerable<T> obj = ValueEx as IEnumerable<T>;
			ValueEx = ((obj != null) ? obj.Concat(newValue).ToArray() : null);
		}
		else
		{
			ValueEx = newValue;
		}
		if (IsValueExRegister)
		{
			ValueExChanged(this, new EventArgs());
		}
	}

	public object GetValue(uint excuteCount, int index, Dictionary<string, string> customConfig = null)
	{
		if (excuteCount != uint.MaxValue && excuteCount != executeCount)
		{
			ValueEx = null;
			return ValueEx;
		}
		if (isConstantValue)
		{
			return Name;
		}
		if (ValueEx is Array && index >= 0)
		{
			if ((ValueEx as Array).Length == 0)
			{
				return null;
			}
			index = ((index < GetValueCount()) ? index : 0);
			object value = (ValueEx as Array).GetValue(index);
			if (value is ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX iMVS_MODULE_STRING_VALUE_EX)
			{
				return iMVS_MODULE_STRING_VALUE_EX.strValue;
			}
			return value;
		}
		if (ValueEx is ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX[])
		{
			return (ValueEx as ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX[]).Select((ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX p) => p.strValue).ToArray();
		}
		return ValueEx;
	}

	public void SetOutputFilter()
	{
		if (Owner != null)
		{
			string customParamName = GetCustomParamName(Owner.ID, Name);
			ServerSetModuleOutput(customParamName);
		}
	}

	public void RemoveOutputFilter()
	{
		if (Owner != null)
		{
			string customParamName = GetCustomParamName(Owner.ID, Name);
			ServerRemoveModuleOutput(customParamName);
		}
	}

	public static string GetCustomParamName(uint id, string name)
	{
		return "[" + id + "]" + name;
	}

	protected virtual void ServerSetOutput(string customName)
	{
	}

	protected virtual void ServerSetModuleOutput(string customName)
	{
	}

	protected virtual void ServerRemoveModuleOutput(string customName)
	{
	}

	public int GetValueCount()
	{
		if (isConstantValue)
		{
			return 1;
		}
		if (ValueEx is Array)
		{
			return (ValueEx as Array).Length;
		}
		return 0;
	}

	public string GetDataPath()
	{
		return "";
	}

	public override string ToString()
	{
		return NameA;
	}
}
