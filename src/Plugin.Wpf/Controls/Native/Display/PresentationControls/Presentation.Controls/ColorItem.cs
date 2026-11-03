using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

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
