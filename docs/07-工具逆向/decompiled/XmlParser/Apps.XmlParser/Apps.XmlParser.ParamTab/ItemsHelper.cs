using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Data;

namespace Apps.XmlParser.ParamTab;

internal class ItemsHelper
{
	public static object GetPropertyValueByName(object obj, string Name)
	{
		PropertyInfo[] properties = obj.GetType().GetProperties();
		PropertyInfo[] array = properties;
		foreach (PropertyInfo propertyInfo in array)
		{
			if (propertyInfo.Name == Name)
			{
				return propertyInfo.GetValue(obj, null);
			}
		}
		return null;
	}

	public static void SetPropertyValueByName(object obj, string Name, object value)
	{
		PropertyInfo[] properties = obj.GetType().GetProperties();
		PropertyInfo[] array = properties;
		foreach (PropertyInfo propertyInfo in array)
		{
			if (propertyInfo.Name == Name)
			{
				propertyInfo.SetValue(obj, value);
				break;
			}
		}
	}

	public static void GetBindingProperty(object source, string BindingPath, Action<object> action)
	{
		BindingObj txt = new BindingObj();
		txt.SetBinding(BindingObj.MyObjProperty, new Binding(BindingPath)
		{
			Mode = BindingMode.TwoWay
		});
		txt.DataContext = source;
		BindingExpression bindingExpression = txt.GetBindingExpression(BindingObj.MyObjProperty);
		txt.OnObjChanged += txt_OnObjChanged;
		bindingExpression.UpdateTarget();
		AutoResetEvent _WaitReseter = new AutoResetEvent(initialState: false);
		txt.Tag = _WaitReseter;
		Thread thread = new Thread(() =>
		{
			_WaitReseter.WaitOne();
			txt.Dispatcher.Invoke(() =>
			{
				if (action != null)
				{
					action(txt.MyObj);
				}
			});
		});
		thread.IsBackground = true;
		thread.Start();
	}

	private static void txt_OnObjChanged(object sender, EventArgs e)
	{
		(sender as BindingObj).OnObjChanged -= txt_OnObjChanged;
		((sender as BindingObj).Tag as AutoResetEvent).Set();
	}

	public static void SetBindingProperty(object source, string BindingPath, object obj)
	{
		FrameworkElement frameworkElement = new FrameworkElement();
		frameworkElement.SetBinding(FrameworkElement.TagProperty, new Binding(BindingPath)
		{
			Mode = BindingMode.OneWayToSource
		});
		frameworkElement.DataContext = source;
		frameworkElement.Tag = obj;
	}
}
