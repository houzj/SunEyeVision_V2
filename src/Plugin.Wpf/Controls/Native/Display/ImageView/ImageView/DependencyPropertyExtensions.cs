using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Reflection;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView;

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
