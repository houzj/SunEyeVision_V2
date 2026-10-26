using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

public class AttachBinding : DependencyObject
{
	public static readonly DependencyProperty BindingStringProperty = DependencyProperty.RegisterAttached("BindingString", typeof(string), typeof(AttachBinding), new PropertyMetadata(DoChangeColorBinding));

	private static void DoChangeColorBinding(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue != null)
		{
			string[] array = e.NewValue.ToString().Split(':');
			if (array.Length > 1 && GetFieldByName(d, array[0] + "Property") is DependencyProperty dp)
			{
				(d as FrameworkElement).SetBinding(dp, new Binding(array[1])
				{
					Mode = BindingMode.TwoWay
				});
			}
		}
	}

	public static object GetFieldByName(object obj, string Name)
	{
		return obj.GetDependencyProperty(Name);
	}

	public static object GetFieldByName(Type type, string Name)
	{
		while (true)
		{
			FieldInfo fieldInfo = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SingleOrDefault((FieldInfo P) => P.Name.Equals(Name));
			DependencyProperty dependencyProperty = ((fieldInfo != null) ? ((DependencyProperty)fieldInfo.GetValue(null)) : null);
			if (dependencyProperty != null)
			{
				return dependencyProperty;
			}
			if (type.GetType().BaseType == null)
			{
				break;
			}
			type = type.BaseType;
		}
		return null;
	}

	public static double GetBindingString(DependencyObject obj)
	{
		return 0.0;
	}

	public static void SetBindingString(DependencyObject obj, double value)
	{
	}
}
