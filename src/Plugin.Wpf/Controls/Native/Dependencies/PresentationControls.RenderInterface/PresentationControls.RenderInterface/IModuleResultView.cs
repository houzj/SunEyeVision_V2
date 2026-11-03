using System;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IModuleResultView
{
	bool IsCanDrawROI { get; }

	event Action<object> ImageChangedEvent;

	void SetRoiVisible();

	void ImageChanged(string strDisplayImageName);
}
