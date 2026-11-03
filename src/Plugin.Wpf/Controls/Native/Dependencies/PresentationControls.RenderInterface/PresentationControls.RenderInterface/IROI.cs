using System.ComponentModel;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IROI : INotifyPropertyChanged
{
	ICommand DataChangedCommand { get; set; }
}
