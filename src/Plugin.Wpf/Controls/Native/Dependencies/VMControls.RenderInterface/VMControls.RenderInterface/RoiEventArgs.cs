using System;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public class RoiEventArgs : EventArgs
{
	public IROI Roi { get; private set; }

	public RoiEventArgs(object roi)
	{
		Roi = roi as IROI;
	}
}
