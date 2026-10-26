using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface ICircleMesROI : IROI, INotifyPropertyChanged
{
	Point CenterPoint { get; set; }

	double Radius { get; set; }
}
