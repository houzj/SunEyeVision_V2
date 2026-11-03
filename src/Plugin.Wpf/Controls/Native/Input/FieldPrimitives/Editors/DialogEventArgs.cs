using SunEyeVision.Plugin.Wpf.Native.Input.FieldPrimitives.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Arranged;
using System;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.FieldPrimitives.Editors;

public class DialogEventArgs : EventArgs
{
	public DialogResult DialogResult { get; set; }

	public SolidColorBrush SelectedColor { get; set; }
}
