using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface ILineCaliperROI : ICaliper, IROI, INotifyPropertyChanged
{
	Point StartPoint { get; set; }

	Point EndPoint { get; set; }

	double CaliperRotateAngle { get; set; }
}
