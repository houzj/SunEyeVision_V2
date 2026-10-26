using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface I3DROI : IROI, INotifyPropertyChanged
{
	double CenterPointX { get; set; }

	double CenterPointY { get; set; }

	double CenterPointZ { get; set; }

	double AngleX { get; set; }

	double AngleY { get; set; }

	double AngleZ { get; set; }
}
