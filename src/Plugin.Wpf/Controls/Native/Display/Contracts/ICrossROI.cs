using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface ICrossROI : IROI, INotifyPropertyChanged
{
	Point CenterPoint { get; set; }
}
