using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedPolygonMaskROI))]
public class NormalizedPolygonMaskROI : NormalizedPolygonROI
{
	private Polygon mPolygon;

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		mPolygon = Template.FindName("mPolygon", this) as Polygon;
		mPolygon.Fill = new BrushConverter().ConvertFromString("#7FFF5400") as Brush;
	}
}
