using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Primitives;

internal struct HsvColor(double h, double s, double v)
{
	public double H = h;

	public double S = s;

	public double V = v;
}
