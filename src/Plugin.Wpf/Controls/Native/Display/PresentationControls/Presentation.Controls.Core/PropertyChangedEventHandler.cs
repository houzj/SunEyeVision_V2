using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core;

public delegate void PropertyChangedEventHandler<T>(object sender, PropertyChangedEventArgs<T> e);
