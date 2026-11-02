using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface ICoordinateROI : IROI, INotifyPropertyChanged
{
	Point OrgPoint { get; set; }

	double Angle { get; set; }
}
