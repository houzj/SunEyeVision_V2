using SunEyeVision.Plugin.Wpf.Native.Dependencies.VisualElements;
using SunEyeVision.Plugin.Wpf.Interop.Windows;
using System;

namespace SunEyeVision.Plugin.Wpf.Interop.Windows;

public class ObjectEventArgs : EventArgs
{
	public object Param { get; set; }
}
