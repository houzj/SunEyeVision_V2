using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Xml;
using Hik.Util;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class Trigger : BaseNode, ITrigger, INotifyPropertyChanged
{
	public string Property;

	public string Value;

	public ObservableCollection<object> Setters;

	public string PropertyA
	{
		get
		{
			return Property;
		}
		set
		{
			Property = value;
		}
	}

	public string ValueA
	{
		get
		{
			return Value;
		}
		set
		{
			Value = value;
		}
	}

	public ObservableCollection<object> SettersA
	{
		get
		{
			return Setters;
		}
		set
		{
			Setters = value;
			OnPropertyChanged("SettersA");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public List<object> GetSetters()
	{
		List<object> list = new List<object>();
		if (Setters != null)
		{
			list.AddRange(Setters);
		}
		return list;
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		if (Setters.Count > 0)
		{
			foreach (Setter setter in Setters)
			{
				setter.CreatOperateInstance();
			}
		}
		return this;
	}

	public bool CheckProperty(string proName)
	{
		return Property + "A" == proName;
	}

	public void ReOnTrigger()
	{
		foreach (Setter setter in Setters)
		{
			if (setter.Operation != null && setter.Operation.Working)
			{
				setter.Operation.ReOperate();
			}
		}
	}

	public void OnTrigger(object obj)
	{
		if (!(obj is NameValueItem nameValueItem))
		{
			return;
		}
		object propertyValueByName = nameValueItem.GetPropertyValueByName(Property + "A");
		object TValue = null;
		if (Setters == null || obj == null)
		{
			return;
		}
		if (Value != null)
		{
			if (propertyValueByName == null)
			{
				return;
			}
			GetValue(ref TValue, propertyValueByName, Value);
			if (TValue == null || !StrArrContain(TValue.ToString().Split('|'), propertyValueByName.ToString()))
			{
				return;
			}
			{
				foreach (Setter setter3 in Setters)
				{
					setter3.Operate(new object[2] { TValue, propertyValueByName }, needThrowException: true);
				}
				return;
			}
		}
		foreach (Setter setter4 in Setters)
		{
			setter4.Operate(new object[2]
			{
				GetParentNode(),
				propertyValueByName
			}, needThrowException: true);
		}
	}

	public static bool StrArrContain(string[] strArr, string str)
	{
		foreach (string text in strArr)
		{
			if (text == str)
			{
				return true;
			}
		}
		return false;
	}

	public static void GetValue(ref object TValue, object obj, string Value)
	{
		switch (Type.GetTypeCode(obj.GetType()))
		{
		case TypeCode.String:
			TValue = Value;
			break;
		case TypeCode.DateTime:
			try
			{
				string format = "yyyyMMddHHmmss";
				IFormatProvider currentCulture = CultureInfo.CurrentCulture;
				TValue = DateTime.ParseExact(Value, format, currentCulture);
				break;
			}
			catch
			{
				TValue = DateTime.MinValue;
				break;
			}
		case TypeCode.Int32:
			try
			{
				TValue = StringUtil.ParseInt(Value);
				break;
			}
			catch
			{
				TValue = int.MinValue;
				break;
			}
		case TypeCode.Single:
			try
			{
				TValue = float.Parse(Value);
				break;
			}
			catch
			{
				try
				{
					TValue = StringUtil.ParseUInt(Value);
					break;
				}
				catch
				{
					TValue = float.NaN;
					break;
				}
			}
		case TypeCode.Boolean:
			TValue = bool.Parse(Value);
			break;
		case TypeCode.Byte:
			TValue = StringUtil.ParseByte(Value);
			break;
		case TypeCode.Int16:
			try
			{
				TValue = StringUtil.ParseShort(Value);
				break;
			}
			catch
			{
				TValue = short.MinValue;
				break;
			}
		case TypeCode.UInt16:
			try
			{
				TValue = StringUtil.ParseUShort(Value);
				break;
			}
			catch
			{
				TValue = (ushort)0;
				break;
			}
		case TypeCode.UInt32:
			try
			{
				TValue = StringUtil.ParseUInt(Value);
				break;
			}
			catch
			{
				TValue = 0u;
				break;
			}
		case TypeCode.Int64:
			try
			{
				TValue = StringUtil.ParseLong(Value);
				break;
			}
			catch
			{
				TValue = long.MinValue;
				break;
			}
		case TypeCode.UInt64:
			try
			{
				TValue = StringUtil.ParseULong(Value);
				break;
			}
			catch
			{
				TValue = 0uL;
				break;
			}
		case TypeCode.Double:
			try
			{
				TValue = double.Parse(Value);
				break;
			}
			catch
			{
				try
				{
					TValue = StringUtil.ParseUInt(Value);
					break;
				}
				catch
				{
					TValue = double.NaN;
					break;
				}
			}
		case TypeCode.Char:
		case TypeCode.SByte:
		case TypeCode.Decimal:
		case (TypeCode)17:
			break;
		}
	}
}
