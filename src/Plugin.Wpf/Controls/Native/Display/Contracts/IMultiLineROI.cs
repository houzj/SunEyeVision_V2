using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IMultiLineROI : IROI, INotifyPropertyChanged
{
	ObservableCollection<Point> Points { get; set; }
}
