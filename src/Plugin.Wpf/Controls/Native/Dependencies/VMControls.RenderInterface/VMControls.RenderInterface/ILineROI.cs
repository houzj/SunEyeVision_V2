using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface ILineROI : IROI, INotifyPropertyChanged
{
	Point StartPoint { get; set; }

	Point EndPoint { get; set; }
}
