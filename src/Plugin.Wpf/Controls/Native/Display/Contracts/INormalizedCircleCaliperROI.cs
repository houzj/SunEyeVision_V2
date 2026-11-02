using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface INormalizedCircleCaliperROI : ICircleCaliperROI, ICaliper, IROI, INotifyPropertyChanged, INormalizedROI
{
}
