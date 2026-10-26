using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class NewItemTypesAttribute : Attribute
{
	public IList<Type> Types { get; set; }

	public NewItemTypesAttribute(params Type[] types)
	{
		Types = new List<Type>(types);
	}

	public NewItemTypesAttribute()
	{
	}
}
