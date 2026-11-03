using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IRoundedRectROI : IRectROI, IROI, INotifyPropertyChanged
{
	double CornerRadius { get; set; }
}
