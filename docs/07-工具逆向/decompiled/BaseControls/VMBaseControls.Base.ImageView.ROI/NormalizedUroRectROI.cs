using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedUroRectROI))]
public class NormalizedUroRectROI : NormalizedRectROI
{
	public NormalizedUroRectROI()
	{
		IsRotatable = false;
	}
}
