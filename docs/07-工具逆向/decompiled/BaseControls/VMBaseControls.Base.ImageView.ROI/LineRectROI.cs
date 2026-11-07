using System.Windows;
using System.Windows.Shapes;
using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(ILineRectROI))]
public class LineRectROI : RectROI
{
	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		Line line = Template.FindName("line", this) as Line;
		line.Visibility = Visibility.Visible;
	}
}
