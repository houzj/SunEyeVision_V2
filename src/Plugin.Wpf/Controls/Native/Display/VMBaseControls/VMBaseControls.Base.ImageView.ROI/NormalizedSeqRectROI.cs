using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

[ROIDisplayForInterfaceType(typeof(INormalizedSeqRectROI))]
public class NormalizedSeqRectROI : NormalizedRectROI
{
	public NormalizedSeqRectROI()
	{
		IsSeqRect = true;
	}
}
