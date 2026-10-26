using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface ICoordinateROI : IROI, INotifyPropertyChanged
{
	Point OrgPoint { get; set; }

	double Angle { get; set; }
}
