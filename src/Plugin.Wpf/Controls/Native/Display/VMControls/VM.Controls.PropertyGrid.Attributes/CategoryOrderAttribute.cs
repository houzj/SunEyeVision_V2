using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class CategoryOrderAttribute : Attribute
{
	public int Order { get; set; }

	public virtual string Category => CategoryValue;

	public string CategoryValue { get; private set; }

	public CategoryOrderAttribute()
	{
	}

	public CategoryOrderAttribute(string categoryName, int order)
		: this()
	{
		CategoryValue = categoryName;
		Order = order;
	}
}
