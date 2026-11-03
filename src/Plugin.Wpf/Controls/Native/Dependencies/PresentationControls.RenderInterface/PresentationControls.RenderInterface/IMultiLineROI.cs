using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IMultiLineROI : IROI, INotifyPropertyChanged
{
	ObservableCollection<Point> Points { get; set; }
}
