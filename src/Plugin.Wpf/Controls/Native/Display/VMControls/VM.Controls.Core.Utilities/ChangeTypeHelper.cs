using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal static class ChangeTypeHelper
{
	internal static object ChangeType(object value, Type conversionType, IFormatProvider provider)
	{
		if (conversionType == null)
		{
			throw new ArgumentNullException("conversionType");
		}
		if (conversionType == typeof(Guid))
		{
			return new Guid(value.ToString());
		}
		if (conversionType == typeof(Guid?))
		{
			if (value == null)
			{
				return null;
			}
			return new Guid(value.ToString());
		}
		if (conversionType.IsGenericType && conversionType.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
		{
			if (value == null)
			{
				return null;
			}
			conversionType = new NullableConverter(conversionType).UnderlyingType;
		}
		return Convert.ChangeType(value, conversionType, provider);
	}
}
