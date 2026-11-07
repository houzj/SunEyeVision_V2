using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Xml;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using Hik.Xml;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser;

public abstract class FeatureBase : BaseParamNode, IOwner, IReset, IGetResultValue, IFeature, IUpdateBridgeBase
{
	[XmlMark(true)]
	public string Mapping;

	[XmlMark(true)]
	public string Value;

	[XmlMark(true)]
	public bool ArrayMatch;

	public float _Value_Float;

	public int _Value_Int;

	public string Type;

	public string DefualtValue;

	private int VmIO_Notnull_Count;

	protected bool? needUpdateMapValue = null;

	public bool IsMultiMappingFlag;

	public bool IsColumeMappingFlag;

	protected bool isMappingValid;

	public string[] MappingArray;

	public string[] ValueArray;

	protected uint? ExecuteCount;

	private object[] ValueStore;

	public List<VmIO> vmIOs = new List<VmIO>();

	public Dictionary<int, int> vmIOKs = new Dictionary<int, int>();

	public bool isFixedValue;

	private int valueStoreSumNum = 0;

	private int maxValueLenth = 0;

	private bool needRebuidValueStore = false;

	protected const string NEED_NOT_REPLACE_STTRING = "NEED_NOT_REPLACE_STTRING";

	protected const string MODULE_DISPLAY_NAME = "{ModuleDisplayName}";

	protected string ParsedValue;

	private string oldLanguageType;

	private object _Owner;

	public const string PrefixKeyName = "FeatureProperty_";

	public float Value_Float => _Value_Float;

	public int Value_Int => _Value_Int;

	private FilterBase MapFilterSource { get; set; }

	public object Owner
	{
		get
		{
			if (_Owner == null)
			{
				IOwner owner = GetTopBaseNode() as IOwner;
				_Owner = owner.Owner;
			}
			return _Owner;
		}
		set
		{
		}
	}

	public string MappingA
	{
		get
		{
			return Mapping;
		}
		set
		{
			if (Mapping != value)
			{
				Mapping = value;
				SaveMapping(value, (GetParentNode() as Object).NameA);
				InitVmIOs();
				OnPropertyChanged("MappingA");
			}
		}
	}

	public bool ArrayMatchA
	{
		get
		{
			return ArrayMatch;
		}
		set
		{
			ArrayMatch = value;
			OnPropertyChanged("ArrayMatchA");
		}
	}

	public bool GetValueByMapping => !string.IsNullOrEmpty(Mapping) && Mapping.ToUpper() != "NULL";

	public virtual string ValueA { get; set; }

	public virtual string MapValueA { get; set; }

	public virtual event EventHandler MatchContentChanged;

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		float.TryParse(Value, out _Value_Float);
		int.TryParse(Value, out _Value_Int);
		if (string.IsNullOrEmpty(DefualtValue))
		{
			DefualtValue = Value;
		}
		ParamRoot paramRoot = GetParentNode<ParamRoot>();
		if (paramRoot != null)
		{
			PropertyChangedEventManager.AddHandler(paramRoot, OwnerChanged, "Owner");
		}
		InitMappingValid();
		return this;
	}

	internal virtual void Initializer()
	{
	}

	private void InitMappingValid()
	{
		isMappingValid = Mapping != null && Mapping != "NULL" && Mapping != "";
	}

	public virtual void InitVmIOs()
	{
		DeInitVmIOs();
		IsMultiMappingFlag = Name == "Content" || Name == "Tips";
		IsColumeMappingFlag = Name == "Colume";
		MappingArray = MappingA?.Split(',') ?? new string[0];
		ValueArray = Value?.Split(',');
		InitMappingValid();
		if (IsMultiMappingFlag)
		{
			string[] mappingArray = MappingArray;
			foreach (string text in mappingArray)
			{
				VmIO mapIO = GetMapIO(text);
				if (mapIO != null)
				{
					if (!vmIOKs.ContainsKey(mapIO.GetHashCode()))
					{
						vmIOs.Add(mapIO);
						vmIOKs.Add(mapIO.GetHashCode(), vmIOKs.Count);
					}
				}
				else if (!needUpdateMapValue.HasValue && !string.IsNullOrEmpty(text) && text.Contains('.'))
				{
					needUpdateMapValue = true;
				}
			}
		}
		else if (IsColumeMappingFlag)
		{
			string[] valueArray = ValueArray;
			foreach (string mapValue in valueArray)
			{
				VmIO mapIO2 = GetMapIO(mapValue);
				if (mapIO2 != null)
				{
					vmIOs.Add(mapIO2);
					vmIOKs.Add(mapIO2.GetHashCode(), vmIOKs.Count);
				}
			}
		}
		else
		{
			VmIO mapIO3 = GetMapIO(Mapping);
			if (mapIO3 == null && (parentNode as Object).Type == "datalist")
			{
				mapIO3 = GetMapIO(Value);
			}
			if (mapIO3 != null)
			{
				vmIOs.Add(mapIO3);
				vmIOKs.Add(mapIO3.GetHashCode(), vmIOKs.Count);
			}
		}
		VmIO_Notnull_Count = vmIOs.Count;
		isFixedValue = VmIO_Notnull_Count == 0;
		SetOutputFilter();
	}

	internal void DeInitVmIOs()
	{
		RemoveOutputFilter();
		vmIOs.Clear();
		vmIOKs.Clear();
	}

	protected void UpdateMapValue()
	{
		needUpdateMapValue = false;
		VmModule vmModule = Owner as VmModule;
		VmModuleSet vmModuleSet = vmModule?.Parent as VmModuleSet;
		if (vmModule == null || vmModuleSet == null)
		{
			return;
		}
		string text = MappingA;
		string[] mappingArray = MappingArray;
		foreach (string map in mappingArray)
		{
			if (string.IsNullOrEmpty(map) || !map.Contains('.'))
			{
				continue;
			}
			VmIO vmIO = vmIOs.FirstOrDefault((VmIO p) => map == $"{p.Root?.ID} {p.Root?.Name}.{p.Name}");
			if (vmIO != null)
			{
				continue;
			}
			string[] array = map.Split('.');
			int num = map.IndexOf(' ', 0);
			if (array.Length != 2 || num <= 0 || array[0].Length <= num + 1)
			{
				continue;
			}
			string sub = array[0].Substring(num + 1);
			int result = -1;
			if (!int.TryParse(array[0].Substring(0, num), out result))
			{
				continue;
			}
			if (vmModuleSet[sub] is VmModule vmModule2)
			{
				if (vmModule2.ID != result)
				{
					text = text?.Replace(array[0], $"{vmModule2.ID} {vmModule2.Name}");
				}
			}
			else if (result >= 10000 && vmModuleSet.ID != result)
			{
				text = text?.Replace(array[0], $"{vmModuleSet.ID} {vmModuleSet.Name}");
			}
		}
		if (text != MappingA)
		{
			MappingA = text;
		}
	}

	private void OwnerChanged(object sender, PropertyChangedEventArgs e)
	{
		InitVmIOs();
	}

	public void Reset()
	{
		ValueA = DefualtValue;
	}

	public bool IsMultiMapping()
	{
		return Name == "Content" || Name == "Tips";
	}

	public bool IsColumeMapping()
	{
		return Name == "Colume";
	}

	public abstract VmModule GetModuleByNodeID(int nodeID);

	public abstract VmIO GetOutputFilter(object node, string mapValue);

	public object GetValue(uint excuteCount, int index, Dictionary<string, string> customConfig = null)
	{
		if (isFixedValue)
		{
			return ValueA;
		}
		if (IsMultiMappingFlag)
		{
			if (!string.IsNullOrEmpty(ValueA))
			{
				string text = ParseValue(ValueA, index);
				if (MappingArray == null)
				{
					return null;
				}
				bool flag = false;
				string[] mappingArray = MappingArray;
				foreach (string text2 in mappingArray)
				{
					string text3 = GetMapIO(text2)?.GetValue(excuteCount, index, customConfig)?.ToString();
					if (text3 == null)
					{
						text3 = "";
					}
					else
					{
						flag = true;
					}
					text = text?.Replace($"{{{Array.IndexOf(MappingArray, text2)}}}", text3);
					if (text2 == "")
					{
						text = text?.Replace("{" + text2 + "}", text3);
					}
				}
				if (!flag && !string.IsNullOrEmpty(MappingA))
				{
					return "";
				}
				return text;
			}
			VmIO mapIO = GetMapIO(Mapping);
			if (mapIO == null && (parentNode as Object).Type == "datalist")
			{
				mapIO = GetMapIO(Value);
			}
			if (mapIO != null)
			{
				if (Type == "Array")
				{
					return mapIO.GetValue(excuteCount, -1);
				}
				return mapIO.GetValue(excuteCount, index);
			}
		}
		else
		{
			if (IsColumeMappingFlag)
			{
				if (ValueArray == null)
				{
					return null;
				}
				object[] array = new object[ValueArray.Length];
				string[] valueArray = ValueArray;
				foreach (string text4 in valueArray)
				{
					VmIO mapIO2 = GetMapIO(text4);
					if (mapIO2 != null)
					{
						array[Array.IndexOf(ValueArray, text4)] = mapIO2.GetValue(excuteCount, index);
					}
				}
				return array;
			}
			VmIO mapIO3 = GetMapIO(Mapping);
			if (mapIO3 == null && (parentNode as Object).Type == "datalist")
			{
				mapIO3 = GetMapIO(Value);
			}
			if (mapIO3 != null)
			{
				if (Type == "Array")
				{
					return mapIO3.GetValue(excuteCount, -1);
				}
				return mapIO3.GetValue(excuteCount, index);
			}
		}
		return Value;
	}

	internal void ReNameInfo(string oldName, string newName)
	{
		SaveValue(null, oldName);
		SaveValue(Value, newName);
		SaveMapping(null, oldName);
		SaveMapping(Mapping, newName);
	}

	protected void SaveValue(string data, string parentName)
	{
		try
		{
			if ((GetTopBaseNode() as ParamRoot).Owner is VmModule { UserData: var userData })
			{
				userData["[ParamRoot]_" + parentName + "." + NameA + ".Value"] = ((data == null) ? null : StringUtil.UTF8GetBytesPadZero(data));
			}
		}
		catch
		{
		}
	}

	protected void SaveMapping(string data, string parentName)
	{
		try
		{
			if ((GetTopBaseNode() as ParamRoot).Owner is VmModule { UserData: var userData })
			{
				userData["[ParamRoot]_" + parentName + "." + NameA + ".Mapping"] = ((data == null) ? null : StringUtil.UTF8GetBytesPadZero(Mapping));
			}
		}
		catch
		{
		}
	}

	internal void ClearPropertyInfo()
	{
		SaveValue(null, (GetParentNode() as Object).NameA);
		SaveMapping(null, (GetParentNode() as Object).NameA);
	}

	public FeatureBase()
	{
		IO_ValueChanged(null, null);
		GetValue(null);
		ParseValue(null, 0);
	}

	public void SetOutputFilter()
	{
		vmIOs.ForEach((VmIO p) =>
		{
			WeakEventManager<VmIO, ValueEventArgs>.AddHandler(p, "ValueChanged", IO_ValueChanged);
		});
	}

	public void RemoveOutputFilter()
	{
		vmIOs.ForEach((VmIO p) =>
		{
			WeakEventManager<VmIO, ValueEventArgs>.RemoveHandler(p, "ValueChanged", IO_ValueChanged);
		});
	}

	private void IO_ValueChanged(object sender, ValueEventArgs valueEventArgs)
	{
		if (sender == null)
		{
			return;
		}
		try
		{
			if (ExecuteCount != valueEventArgs.ExecuteCount || ValueStore == null || needRebuidValueStore)
			{
				ExecuteCount = valueEventArgs.ExecuteCount;
				ValueStore = new object[vmIOs.Count];
				valueStoreSumNum = 0;
				maxValueLenth = 0;
				needRebuidValueStore = false;
			}
			if (valueEventArgs.Value is Array array)
			{
				int num = vmIOKs[sender.GetHashCode()];
				if (ValueStore[num] == null)
				{
					valueStoreSumNum++;
				}
				ValueStore[num] = array;
				int val = array.Length;
				if (Type == "Array")
				{
					val = 1;
				}
				maxValueLenth = Math.Max(maxValueLenth, val);
			}
			if (VmIO_Notnull_Count == valueStoreSumNum)
			{
				object value = GetValue(ValueStore, maxValueLenth, haveMaxLenth: true);
				(parentNode as Object)?.FeatureNotifyValueChanged(this, ExecuteCount.Value, value, maxValueLenth);
				ClearValueStore();
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("异常信息：" + ex.Message);
			LogHelper.Error("堆栈信息：" + ex.StackTrace);
		}
	}

	internal void ClearValueStore()
	{
		needRebuidValueStore = true;
	}

	public object GetValue(object[] values, int maxLenth = 0, bool haveMaxLenth = false, Dictionary<string, string> customConfig = null)
	{
		if (values == null || values.Length == 0)
		{
			return null;
		}
		if (IsMultiMappingFlag)
		{
			bool flag = false;
			int num = (haveMaxLenth ? maxLenth : values.Max((object p) => (p as Array)?.Length ?? 0));
			string[] array = new string[num];
			for (int num2 = 0; num2 < num; num2++)
			{
				string text = ParseValue(ValueA, num2, isCallBackByVmIO: true);
				bool flag2 = false;
				for (int num3 = 0; num3 < values.Length; num3++)
				{
					object obj = values[num3];
					string text2 = null;
					if (obj is Array array2)
					{
						int index = ((num2 < array2.Length) ? num2 : 0);
						object value = array2.GetValue(index);
						if (value is ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX iMVS_MODULE_STRING_VALUE_EX)
						{
							text2 = iMVS_MODULE_STRING_VALUE_EX.strValue;
							flag = true;
						}
						else if (value is ImvsSdkDefine.IMVS_MODULE_BYTEDATA_VALUE_EX_Data iMVS_MODULE_BYTEDATA_VALUE_EX_Data)
						{
							text2 = iMVS_MODULE_BYTEDATA_VALUE_EX_Data.strValue;
							flag = true;
						}
						else if (value != null)
						{
							text2 = value.ToString();
							flag = true;
						}
					}
					if (text2 != null)
					{
						flag2 = true;
					}
					int num4 = num3;
					if (vmIOs.Count != MappingArray.Length && vmIOs.Count > num3)
					{
						string name = vmIOs[num3].Name;
						if (MappingArray.Contains(name))
						{
							num4 = Array.IndexOf(MappingArray, name);
						}
						else
						{
							string value2 = vmIOs[num3].Root.ID + " " + vmIOs[num3].Root.Name + "." + vmIOs[num3].Name;
							if (MappingArray.Contains(value2))
							{
								num4 = Array.IndexOf(MappingArray, value2);
							}
						}
					}
					text = text?.Replace($"{{{num4}}}", text2);
					if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text2))
					{
						text = text2;
					}
				}
				if (!flag2 && !string.IsNullOrEmpty(MappingA))
				{
					text = "";
				}
				array[num2] = text;
			}
			if (!flag && !string.IsNullOrEmpty(MappingA))
			{
				return new string[1] { "" };
			}
			return array;
		}
		if (IsColumeMappingFlag)
		{
			int num5 = (haveMaxLenth ? maxLenth : ((values.FirstOrDefault((object p) => p is Array) as Array)?.Length ?? 0));
			object[] array3 = new object[num5];
			for (int num6 = 0; num6 < num5; num6++)
			{
				object[] array4 = new object[values.Length];
				foreach (object obj2 in values)
				{
					if (!(obj2 is Array array5))
					{
						continue;
					}
					int num8 = ((num6 < array5.Length) ? num6 : 0);
					if ((obj2 as Array).Length > num8)
					{
						object value3 = array5.GetValue(num8);
						if (value3 is ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX iMVS_MODULE_STRING_VALUE_EX2)
						{
							array4[Array.IndexOf(values, obj2)] = iMVS_MODULE_STRING_VALUE_EX2.strValue;
						}
						else if (value3 is ImvsSdkDefine.IMVS_MODULE_BYTEDATA_VALUE_EX_Data iMVS_MODULE_BYTEDATA_VALUE_EX_Data2)
						{
							array4[Array.IndexOf(values, obj2)] = iMVS_MODULE_BYTEDATA_VALUE_EX_Data2.strValue;
						}
						else
						{
							array4[Array.IndexOf(values, obj2)] = value3;
						}
					}
				}
				array3[num6] = array4;
			}
			return array3;
		}
		if (values[0] is ImvsSdkDefine.IMVS_MODULE_STRING_VALUE_EX iMVS_MODULE_STRING_VALUE_EX3)
		{
			return iMVS_MODULE_STRING_VALUE_EX3.strValue;
		}
		return values[0];
	}

	public int GetValueCount()
	{
		if (IsMultiMappingFlag)
		{
			int num = 1;
			if (MappingArray != null)
			{
				string[] mappingArray = MappingArray;
				foreach (string mapValue in mappingArray)
				{
					VmIO mapIO = GetMapIO(mapValue);
					if (mapIO != null)
					{
						num = Math.Max(num, mapIO.GetValueCount());
					}
				}
			}
			return num;
		}
		if (IsColumeMappingFlag)
		{
			int num2 = 0;
			string[] valueArray = ValueArray;
			foreach (string mapValue2 in valueArray)
			{
				VmIO mapIO2 = GetMapIO(mapValue2);
				if (mapIO2 != null)
				{
					num2 = Math.Max(num2, mapIO2.GetValueCount());
				}
			}
			return num2;
		}
		VmIO mapIO3 = GetMapIO(Mapping);
		if (mapIO3 == null && (parentNode as Object).Type == "datalist")
		{
			mapIO3 = GetMapIO(Value);
		}
		if (mapIO3 != null)
		{
			if (Type == "Array")
			{
				return 1;
			}
			return mapIO3.GetValueCount();
		}
		return 0;
	}

	private string ParseValue(string value, int index, bool isCallBackByVmIO = false)
	{
		if (value == null)
		{
			return null;
		}
		if (oldLanguageType != AppLocalizationService.GetLangType())
		{
			Initializer();
			RefreshParseValue();
			oldLanguageType = AppLocalizationService.GetLangType();
		}
		return ParsedValue.Replace("NEED_NOT_REPLACE_STTRING", index.ToString());
	}

	protected virtual void RefreshParseValue()
	{
	}

	protected string[] GetIndexMap(string value)
	{
		string[] array = value.Split('{');
		string[] array2 = new string[array.Length];
		if (array.Length != 0)
		{
			array2[0] = array[0];
		}
		int num = 1;
		for (int i = 1; i < array.Length; i++)
		{
			if (array[i].Contains('}'))
			{
				array2[num] += array[i];
				num++;
			}
			else
			{
				array2[num] += array[i];
				array2[num] += "{";
			}
		}
		return array2;
	}

	public VmIO GetMapIO(string mapValue)
	{
		if (mapValue == null)
		{
			return null;
		}
		if (mapValue == "NULL")
		{
			return null;
		}
		string[] array = mapValue.Split('.');
		VmModule node = Owner as VmModule;
		if (array.Length > 1)
		{
			return node.GetIoByPathFromAround(mapValue);
		}
		VmIO vmIO = vmIOs.FirstOrDefault((VmIO p) => p.Name == mapValue);
		if (vmIO == null)
		{
			vmIO = GetOutputFilter(node, mapValue);
		}
		if (vmIO != null)
		{
			return vmIO;
		}
		return null;
	}

	public override string ToString()
	{
		return NameA;
	}

	public void UpdateAllUserData()
	{
	}

	public void UpdateSignleBase(string key, IRefreshBridgeObject brigeBase)
	{
	}
}
