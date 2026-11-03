using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IEllipseROI : IROI, INotifyPropertyChanged
{
	Point CenterPoint { get; set; }

	double RadiusX { get; set; }

	double RadiusY { get; set; }

	double Angle { get; set; }
}
