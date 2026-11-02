using System.ComponentModel;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IROI : INotifyPropertyChanged
{
	ICommand DataChangedCommand { get; set; }
}
