using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface ICrossROI : IROI, INotifyPropertyChanged
{
	Point CenterPoint { get; set; }
}
