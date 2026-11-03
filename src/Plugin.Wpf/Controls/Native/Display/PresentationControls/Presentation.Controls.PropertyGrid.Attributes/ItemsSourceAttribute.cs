using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Attributes;

public class ItemsSourceAttribute : Attribute
{
	public Type Type { get; set; }

	public ItemsSourceAttribute(Type type)
	{
		if (type.GetInterface(typeof(IItemsSource).FullName) == null)
		{
			throw new ArgumentException("Type must implement the IItemsSource interface.", "type");
		}
		Type = type;
	}
}
