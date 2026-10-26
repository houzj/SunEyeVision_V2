using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
public class PropertyOrderAttribute : Attribute
{
	public int Order { get; set; }

	public UsageContextEnum UsageContext { get; set; }

	public override object TypeId => this;

	public PropertyOrderAttribute(int order)
		: this(order, UsageContextEnum.Both)
	{
	}

	public PropertyOrderAttribute(int order, UsageContextEnum usageContext)
	{
		Order = order;
		UsageContext = usageContext;
	}
}
