using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IOverSectorROI : IROI, INotifyPropertyChanged
{
	Point CenterPoint { get; set; }

	double Radius { get; set; }

	double MinRadius { get; set; }

	double StartAngle { get; set; }

	double AngleRange { get; set; }

	bool IsClockwise { get; set; }
}
