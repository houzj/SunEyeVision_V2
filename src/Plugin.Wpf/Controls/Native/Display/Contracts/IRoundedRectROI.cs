using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IRoundedRectROI : IRectROI, IROI, INotifyPropertyChanged
{
	double CornerRadius { get; set; }
}
