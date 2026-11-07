using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class MultiTrigger : BaseNode, ITrigger, INotifyPropertyChanged
{
	public string LogicCondition;

	public ObservableCollection<object> Conditions;

	public ObservableCollection<object> Setters;

	public ObservableCollection<object> ConditionsA
	{
		get
		{
			return Conditions;
		}
		set
		{
			Conditions = value;
			OnPropertyChanged("ConditionsA");
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
		if (Conditions != null)
		{
			foreach (Condition condition in Conditions)
			{
				if (condition.Property + "A" == proName)
				{
					return true;
				}
			}
		}
		return false;
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
		if (!(obj is NameValueItem nameValueItem) || Conditions == null)
		{
			return;
		}
		bool flag = false;
		foreach (Condition condition in Conditions)
		{
			object obj2 = null;
			if (condition == null)
			{
				continue;
			}
			string[] array = condition.Property.Split('.');
			string text = "";
			if (2 == array.Length)
			{
				text = array[0];
				NameValueItem nameValueItemByPropertyName = GetNameValueItemByPropertyName(text);
				if (nameValueItemByPropertyName == null)
				{
					return;
				}
				obj2 = nameValueItemByPropertyName.GetPropertyValueByName(array[1] + "A");
			}
			else if (1 == array.Length)
			{
				obj2 = nameValueItem.GetPropertyValueByName(condition.Property + "A");
			}
			object TValue = null;
			if (obj2 == null)
			{
				continue;
			}
			if (condition.Value == null)
			{
				return;
			}
			Trigger.GetValue(ref TValue, obj2, condition.Value);
			if (TValue == null)
			{
				return;
			}
			if (string.IsNullOrEmpty(LogicCondition) || LogicCondition.ToLower() == "and")
			{
				if (TValue.ToString() != obj2.ToString())
				{
					return;
				}
			}
			else if (!string.IsNullOrEmpty(LogicCondition) && LogicCondition.ToLower() == "or" && TValue.ToString() == obj2.ToString())
			{
				flag = true;
				break;
			}
		}
		if (!string.IsNullOrEmpty(LogicCondition) && LogicCondition.ToLower() == "or" && !flag)
		{
			return;
		}
		foreach (Setter setter in Setters)
		{
			setter.Operate(new object[1] { obj }, needThrowException: true);
		}
	}

	private NameValueItem GetNameValueItemByPropertyName(string nodeName)
	{
		if (string.IsNullOrWhiteSpace(nodeName))
		{
			return null;
		}
		BaseNode baseNode = null;
		baseNode = ((!(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)) ? GetParentNode<Tab>()?.FindBaseNodeByName(nodeName) : algorithmTabRoot.FindBaseNodeByName(nodeName));
		if (baseNode != null)
		{
			return baseNode as NameValueItem;
		}
		return null;
	}
}
