using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface INormalizedLineCaliperROI : ILineCaliperROI, ICaliper, IROI, INotifyPropertyChanged, INormalizedROI
{
}
