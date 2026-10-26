using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IMultiLineROI : IROI, INotifyPropertyChanged
{
	ObservableCollection<Point> Points { get; set; }
}
