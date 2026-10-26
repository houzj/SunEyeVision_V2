using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IMatrixCircleROI : IRectROI, IROI, INotifyPropertyChanged
{
	uint M { get; set; }

	uint N { get; set; }

	double Radius { get; set; }

	double MinRadius { get; set; }
}
