using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IPolygonROI : IROI, INotifyPropertyChanged
{
	ObservableCollection<Point> PolygonPoints { get; set; }
}
