using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class ColorItem
{
	public Color? Color { get; set; }

	public string Name { get; set; }

	public ColorItem(Color? color, string name)
	{
		Color = color;
		Name = name;
	}

	public override bool Equals(object obj)
	{
		if (!(obj is ColorItem { Color: var color } colorItem))
		{
			return false;
		}
		if (color.Equals(Color))
		{
			return colorItem.Name.Equals(Name);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return Color.GetHashCode() ^ Name.GetHashCode();
	}
}
