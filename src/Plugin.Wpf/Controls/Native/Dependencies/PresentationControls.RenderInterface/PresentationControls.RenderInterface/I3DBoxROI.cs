using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface I3DBoxROI : I3DROI, IROI, INotifyPropertyChanged
{
	float Length { get; set; }

	float Width { get; set; }

	float Height { get; set; }
}
