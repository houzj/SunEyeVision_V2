using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base.ImageView.ROI;

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
