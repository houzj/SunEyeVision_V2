using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface INormalizedSectorCaliperROI : ISectorCaliperROI, IROI, INotifyPropertyChanged, ICaliper, INormalizedROI
{
}
