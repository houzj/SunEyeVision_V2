namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public class RoiWithTagEventArgs : RoiEventArgs
{
	public object Tag { get; private set; }

	public RoiWithTagEventArgs(object roi, object tag)
		: base(roi)
	{
		Tag = tag;
	}
}
