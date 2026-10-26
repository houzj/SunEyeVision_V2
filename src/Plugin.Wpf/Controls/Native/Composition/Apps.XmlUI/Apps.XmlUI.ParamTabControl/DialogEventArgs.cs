using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
using System;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;

public class DialogEventArgs : EventArgs
{
	public DialogResult DialogResult { get; set; }

	public SolidColorBrush SelectedColor { get; set; }
}
