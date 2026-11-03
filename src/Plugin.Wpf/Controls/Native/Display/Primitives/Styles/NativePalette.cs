using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Styles;

public static class NativePalette
{
	public static readonly Brush Blue = new SolidColorBrush(Color.FromRgb(63, 133, 228));

	public static readonly Brush PressBlue = new SolidColorBrush(Color.FromRgb(49, 111, 194));

	public static readonly Brush HoverBlue = new SolidColorBrush(Color.FromRgb(34, 105, 175));

	public static readonly Brush FontGray = new SolidColorBrush(Color.FromRgb(154, 154, 154));

	public static readonly Brush FontDark = new SolidColorBrush(Color.FromRgb(76, 76, 76));

	public static readonly Brush ControlLight = new SolidColorBrush(Color.FromRgb(250, 250, 250));

	public static readonly Brush ControlGray = new SolidColorBrush(Color.FromRgb(209, 213, 218));
}
