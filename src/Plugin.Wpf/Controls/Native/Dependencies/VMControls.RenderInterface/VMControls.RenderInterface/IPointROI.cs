using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IPointROI : IROI, INotifyPropertyChanged
{
	Point CenterPoint { get; set; }
}
