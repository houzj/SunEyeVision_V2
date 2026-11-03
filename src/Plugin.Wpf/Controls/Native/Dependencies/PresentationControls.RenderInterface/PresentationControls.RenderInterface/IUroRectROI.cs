using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IUroRectROI : IROI, INotifyPropertyChanged
{
	Point CenterPoint { get; set; }

	double Width { get; set; }

	double Height { get; set; }
}
