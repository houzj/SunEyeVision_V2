using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface ILineCaliperROI : ICaliper, IROI, INotifyPropertyChanged
{
	Point StartPoint { get; set; }

	Point EndPoint { get; set; }

	double CaliperRotateAngle { get; set; }
}
