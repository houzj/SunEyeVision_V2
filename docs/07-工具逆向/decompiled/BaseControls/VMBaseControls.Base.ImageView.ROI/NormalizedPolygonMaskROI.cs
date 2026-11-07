using System.Windows.Media;
using System.Windows.Shapes;
using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

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
