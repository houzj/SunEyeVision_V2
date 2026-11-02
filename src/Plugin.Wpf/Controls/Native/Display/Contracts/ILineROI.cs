using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface ILineROI : IROI, INotifyPropertyChanged
{
	Point StartPoint { get; set; }

	Point EndPoint { get; set; }
}
