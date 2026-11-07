using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedSeqRectROI))]
public class NormalizedSeqRectROI : NormalizedRectROI
{
	public NormalizedSeqRectROI()
	{
		IsSeqRect = true;
	}
}
