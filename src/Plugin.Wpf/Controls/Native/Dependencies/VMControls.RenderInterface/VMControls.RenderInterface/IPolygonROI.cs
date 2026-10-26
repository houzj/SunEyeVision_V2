using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IPolygonROI : IROI, INotifyPropertyChanged
{
	ObservableCollection<Point> PolygonPoints { get; set; }
}
