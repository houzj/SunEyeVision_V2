using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Reflection;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

public static class DependencyPropertyExtensions
{
	public static DependencyProperty GetDependencyProperty<T>(this T source, string dpName) where T : class
	{
		return GetDependencyProperty(source.GetType(), dpName);
	}

	private static DependencyProperty GetDependencyProperty(Type type, string name)
	{
		while (true)
		{
			FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.Public);
			DependencyProperty dependencyProperty = ((field != null) ? ((DependencyProperty)field.GetValue(null)) : null);
			if (dependencyProperty != null)
			{
				return dependencyProperty;
			}
			if (type.BaseType == null)
			{
				break;
			}
			type = type.BaseType;
		}
		return null;
	}
}
