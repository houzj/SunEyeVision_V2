using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IPointROI : IROI, INotifyPropertyChanged
{
	Point CenterPoint { get; set; }
}
