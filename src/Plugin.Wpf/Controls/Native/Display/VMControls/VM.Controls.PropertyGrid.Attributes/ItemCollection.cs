using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Attributes;

public class ItemCollection : List<Item>
{
	public void Add(object value)
	{
		Item item = new Item();
		item.DisplayName = value.ToString();
		item.Value = value;
		base.Add(item);
	}

	public void Add(object value, string displayName)
	{
		Item item = new Item();
		item.DisplayName = displayName;
		item.Value = value;
		base.Add(item);
	}
}
