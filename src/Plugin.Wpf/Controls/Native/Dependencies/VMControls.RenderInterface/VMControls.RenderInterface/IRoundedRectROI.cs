using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IRoundedRectROI : IRectROI, IROI, INotifyPropertyChanged
{
	double CornerRadius { get; set; }
}
