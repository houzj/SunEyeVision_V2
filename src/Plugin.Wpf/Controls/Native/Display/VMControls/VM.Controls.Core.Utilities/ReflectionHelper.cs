using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal static class ReflectionHelper
{
	[Conditional("DEBUG")]
	internal static void ValidatePublicPropertyName(object sourceObject, string propertyName)
	{
		if (sourceObject == null)
		{
			throw new ArgumentNullException("sourceObject");
		}
		if (propertyName == null)
		{
			throw new ArgumentNullException("propertyName");
		}
	}

	[Conditional("DEBUG")]
	internal static void ValidatePropertyName(object sourceObject, string propertyName)
	{
		if (sourceObject == null)
		{
			throw new ArgumentNullException("sourceObject");
		}
		if (propertyName == null)
		{
			throw new ArgumentNullException("propertyName");
		}
	}

	internal static bool TryGetEnumDescriptionAttributeValue(Enum enumeration, out string description)
	{
		try
		{
			if (enumeration.GetType().GetField(enumeration.ToString()).GetCustomAttributes(typeof(DescriptionAttribute), inherit: true) is DescriptionAttribute[] array && array.Length != 0)
			{
				description = array[0].Description;
				return true;
			}
		}
		catch
		{
		}
		description = string.Empty;
		return false;
	}

	[DebuggerStepThrough]
	internal static string GetPropertyOrFieldName(MemberExpression expression)
	{
		if (!TryGetPropertyOrFieldName(expression, out var propertyOrFieldName))
		{
			throw new InvalidOperationException("Unable to retrieve the property or field name.");
		}
		return propertyOrFieldName;
	}

	[DebuggerStepThrough]
	internal static string GetPropertyOrFieldName<TMember>(Expression<Func<TMember>> expression)
	{
		if (!TryGetPropertyOrFieldName(expression, out var propertyOrFieldName))
		{
			throw new InvalidOperationException("Unable to retrieve the property or field name.");
		}
		return propertyOrFieldName;
	}

	[DebuggerStepThrough]
	internal static bool TryGetPropertyOrFieldName(MemberExpression expression, out string propertyOrFieldName)
	{
		propertyOrFieldName = null;
		if (expression == null)
		{
			return false;
		}
		propertyOrFieldName = expression.Member.Name;
		return true;
	}

	[DebuggerStepThrough]
	internal static bool TryGetPropertyOrFieldName<TMember>(Expression<Func<TMember>> expression, out string propertyOrFieldName)
	{
		propertyOrFieldName = null;
		if (expression == null)
		{
			return false;
		}
		return TryGetPropertyOrFieldName(expression.Body as MemberExpression, out propertyOrFieldName);
	}

	public static bool IsPublicInstanceProperty(Type type, string propertyName)
	{
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
		return type.GetProperty(propertyName, bindingAttr) != null;
	}
}
