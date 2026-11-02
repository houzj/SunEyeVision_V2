using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface INormalizedPolygonCaliperROI : IPolygonCaliperROI, IRectROI, IROI, INotifyPropertyChanged, INormalizedROI
{
}
