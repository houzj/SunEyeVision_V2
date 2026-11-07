using VMControls.RenderInterface;

namespace VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(ISeqRectROI))]
public class SeqRectROI : RectROI
{
	public SeqRectROI()
	{
		IsSeqRect = true;
	}
}
