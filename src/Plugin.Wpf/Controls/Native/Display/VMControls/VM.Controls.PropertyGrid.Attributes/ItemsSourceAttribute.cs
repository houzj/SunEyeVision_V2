using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Attributes;

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
