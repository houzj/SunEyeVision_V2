using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Apps.Interface;
using Hik.Xml;

namespace Apps.XmlParser;

public class BaseParamNode : BaseNode, INotifyPropertyChanged, IName, ISaveNode
{
	[XmlMark(true)]
	public string Name;

	public virtual string NameA
	{
		get
		{
			return Name;
		}
		set
		{
			if (Name != value && CheckName(value))
			{
				Name = value;
				OnPropertyChanged("NameA");
			}
		}
	}

	public List<object> EnumEntrysA => null;

	string IName.Name
	{
		get
		{
			return Name;
		}
		set
		{
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public virtual bool CheckName(string newName)
	{
		return true;
	}

	public object GetPropertyValueByName(string Name)
	{
		PropertyInfo[] properties = GetType().GetProperties();
		PropertyInfo[] array = properties;
		foreach (PropertyInfo propertyInfo in array)
		{
			if (propertyInfo.Name == Name)
			{
				return propertyInfo.GetValue(this, null);
			}
		}
		return null;
	}

	public void SetPropertyValueByName(string Name, object value)
	{
		PropertyInfo[] properties = GetType().GetProperties();
		PropertyInfo[] array = properties;
		foreach (PropertyInfo propertyInfo in array)
		{
			if (propertyInfo.Name == Name)
			{
				propertyInfo.SetValue(this, value);
				break;
			}
		}
	}

	public void SetFiledValueByName(string Name, object value)
	{
		GetType().GetFields().FirstOrDefault((FieldInfo x) => x.Name == Name).SetValue(this, value);
	}

	public void SetFiledValueByObject(string Name, object sourceObject)
	{
		object value = sourceObject.GetType().GetFields().FirstOrDefault((FieldInfo x) => x.Name == Name)
			.GetValue(sourceObject);
		GetType().GetFields().FirstOrDefault((FieldInfo x) => x.Name == Name).SetValue(this, value);
	}

	public string GetSaveString(NodePathType mode)
	{
		return "";
	}
}
