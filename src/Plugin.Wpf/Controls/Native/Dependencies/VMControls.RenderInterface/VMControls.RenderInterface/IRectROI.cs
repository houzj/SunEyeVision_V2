using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IRectROI : IROI, INotifyPropertyChanged
{
	Point CenterPoint { get; set; }

	double Width { get; set; }

	double Height { get; set; }

	double Angle { get; set; }
}
