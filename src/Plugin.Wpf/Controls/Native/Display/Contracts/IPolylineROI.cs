using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IPolylineROI : IROI, INotifyPropertyChanged
{
	ObservableCollection<Point> PolylinePoints { get; set; }
}
