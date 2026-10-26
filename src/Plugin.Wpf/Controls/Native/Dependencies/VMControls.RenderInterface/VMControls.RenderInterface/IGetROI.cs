using System.Collections.Generic;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IGetROI
{
	void GetRoiList(int imageWidth, int imageHeight, List<IROI> roiResults);

	void SetRoiList(int imageWidth, int imageHeight, List<IROI> roiInputs);
}
