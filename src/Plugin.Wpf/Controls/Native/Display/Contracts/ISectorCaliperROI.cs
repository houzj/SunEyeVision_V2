using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface ISectorCaliperROI : IROI, INotifyPropertyChanged, ICaliper
{
	Point StartPoint { get; set; }

	Point EndPoint { get; set; }

	double Radius { get; set; }

	bool IsLargeArc { get; set; }

	bool IsClockwise { get; set; }
}
