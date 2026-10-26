using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface ISectorROI : IROI, INotifyPropertyChanged
{
	Point CenterPoint { get; set; }

	double Radius { get; set; }

	double MinRadius { get; set; }

	double StartAngle { get; set; }

	double EndAngle { get; set; }

	double AngleRange { get; set; }
}
