using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Coordinates;
using System;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;

public class DialogEventArgs : EventArgs
{
	public DialogResult DialogResult { get; set; }

	public SolidColorBrush SelectedColor { get; set; }
}
