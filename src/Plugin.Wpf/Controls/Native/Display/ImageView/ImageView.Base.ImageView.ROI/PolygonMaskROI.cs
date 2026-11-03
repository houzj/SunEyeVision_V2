using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(IPolygonMaskROI))]
public class PolygonMaskROI : PolygonROI
{
	private Polygon mPolygon;

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		mPolygon = Template.FindName("mPolygon", this) as Polygon;
		mPolygon.Fill = new BrushConverter().ConvertFromString("#7FFF5400") as Brush;
	}
}
