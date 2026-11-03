using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Attributes;

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
