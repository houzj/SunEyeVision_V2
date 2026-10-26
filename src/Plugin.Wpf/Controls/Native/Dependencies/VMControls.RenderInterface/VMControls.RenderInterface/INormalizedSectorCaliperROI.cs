using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface INormalizedSectorCaliperROI : ISectorCaliperROI, IROI, INotifyPropertyChanged, ICaliper, INormalizedROI
{
}
