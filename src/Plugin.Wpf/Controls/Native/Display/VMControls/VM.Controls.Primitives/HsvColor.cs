using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

internal struct HsvColor(double h, double s, double v)
{
	public double H = h;

	public double S = s;

	public double V = v;
}
