using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface ICircleCaliperROI : ICaliper, IROI, INotifyPropertyChanged
{
	double StartAngle { get; set; }

	Point CenterPoint { get; set; }

	double Radius { get; set; }
}
