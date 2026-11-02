using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public class RoiEventArgs : EventArgs
{
	public IROI Roi { get; private set; }

	public RoiEventArgs(object roi)
	{
		Roi = roi as IROI;
	}
}
