using System.Collections.Generic;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IGetROI
{
	void GetRoiList(int imageWidth, int imageHeight, List<IROI> roiResults);

	void SetRoiList(int imageWidth, int imageHeight, List<IROI> roiInputs);
}
