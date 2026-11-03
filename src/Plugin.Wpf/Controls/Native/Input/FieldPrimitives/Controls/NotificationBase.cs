using SunEyeVision.Plugin.Wpf.Native.Input.FieldPrimitives.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Spinners;
using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Input.FieldPrimitives.Controls;

public class NotificationBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}
}
