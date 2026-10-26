using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;
using System;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView;

public class DialogEventArgs : EventArgs
{
	public DialogResult DialogResult { get; set; }

	public SolidColorBrush SelectedColor { get; set; }
}
