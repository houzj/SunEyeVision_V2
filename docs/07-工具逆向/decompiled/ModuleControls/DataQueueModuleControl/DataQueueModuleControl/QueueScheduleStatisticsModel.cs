using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;

namespace DataQueueModuleControl;

public class QueueScheduleStatisticsModel : DynamicObject, INotifyPropertyChanged
{
	private Dictionary<string, object> Properties = new Dictionary<string, object>();

	public event PropertyChangedEventHandler PropertyChanged;

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public object GetPropertyValue(string propertyName)
	{
		if (Properties.TryGetValue(propertyName, out var value))
		{
			return value;
		}
		return null;
	}

	public void SetPropertyValue(string propertyName, object value)
	{
		if (Properties.ContainsKey(propertyName))
		{
			Properties[propertyName] = value;
			NotifyPropertyChanged(propertyName);
		}
		else
		{
			Properties.Add(propertyName, value);
		}
	}

	public override string ToString()
	{
		return Properties.Values.Aggregate("", (string current, object value) => current + $"{value:f3} ");
	}

	public override bool TrySetMember(SetMemberBinder binder, object value)
	{
		if (!Properties.Keys.Contains(binder.Name))
		{
			Properties.Add(binder.Name, value.ToString());
		}
		return true;
	}

	public override bool TryGetMember(GetMemberBinder binder, out object result)
	{
		return Properties.TryGetValue(binder.Name, out result);
	}
}
