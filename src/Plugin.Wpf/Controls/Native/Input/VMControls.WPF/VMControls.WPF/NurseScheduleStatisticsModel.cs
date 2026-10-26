using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF;

public class NurseScheduleStatisticsModel : DynamicObject, INotifyPropertyChanged
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

	/// <summary>
	/// 获取属性值
	/// </summary>
	/// <param name="propertyName"></param>
	/// <returns></returns>
	public object GetPropertyValue(string propertyName)
	{
		if (Properties.TryGetValue(propertyName, out var value))
		{
			return value;
		}
		return null;
	}

	/// <summary>
	/// 设置属性值
	/// </summary>
	/// <param name="propertyName"></param>
	/// <param name="value"></param>
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

	/// <summary>
	/// 设置值
	/// </summary>
	/// <param name="binder"></param>
	/// <param name="value"></param>
	/// <returns></returns>
	public override bool TrySetMember(SetMemberBinder binder, object value)
	{
		if (!Properties.Keys.Contains(binder.Name))
		{
			Properties.Add(binder.Name, value.ToString());
		}
		return true;
	}

	/// <summary>
	/// 获取值
	/// </summary>
	/// <param name="binder"></param>
	/// <param name="result"></param>
	/// <returns></returns>
	public override bool TryGetMember(GetMemberBinder binder, out object result)
	{
		return Properties.TryGetValue(binder.Name, out result);
	}
}
