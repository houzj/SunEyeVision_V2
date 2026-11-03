using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Primitives;

internal struct HsvColor(double h, double s, double v)
{
	public double H = h;

	public double S = s;

	public double V = v;
}
