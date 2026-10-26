using System;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IModuleResultView
{
	bool IsCanDrawROI { get; }

	event Action<object> ImageChangedEvent;

	void SetRoiVisible();

	void ImageChanged(string strDisplayImageName);
}
