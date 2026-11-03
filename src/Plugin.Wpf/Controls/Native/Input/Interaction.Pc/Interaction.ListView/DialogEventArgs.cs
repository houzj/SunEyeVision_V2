using SunEyeVision.Plugin.Wpf.Native.Input.Interaction.ListView;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Interaction.ListView;
using System;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.Interaction.ListView;

public class DialogEventArgs : EventArgs
{
	public DialogResult DialogResult { get; set; }

	public SolidColorBrush SelectedColor { get; set; }
}
