using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Attributes;

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
