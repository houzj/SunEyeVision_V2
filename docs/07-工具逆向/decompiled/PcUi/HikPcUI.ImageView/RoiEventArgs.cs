using System;

namespace HikPcUI.ImageView;

public class RoiEventArgs : EventArgs
{
	public RoiParameter Roi { get; private set; }

	public RoiEventArgs(object roi)
	{
		Roi = roi as RoiParameter;
	}
}
