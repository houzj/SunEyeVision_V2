using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.HikVision.Styles;

public static class HikColors
{
	public static readonly Brush HikBlue = new SolidColorBrush(Color.FromRgb(63, 133, 228));

	public static readonly Brush HikPressBlue = new SolidColorBrush(Color.FromRgb(49, 111, 194));

	public static readonly Brush HikHoverBlue = new SolidColorBrush(Color.FromRgb(34, 105, 175));

	public static readonly Brush HikFontGray = new SolidColorBrush(Color.FromRgb(154, 154, 154));

	public static readonly Brush HikFontDark = new SolidColorBrush(Color.FromRgb(76, 76, 76));

	public static readonly Brush HikControlLight = new SolidColorBrush(Color.FromRgb(250, 250, 250));

	public static readonly Brush HikControlGray = new SolidColorBrush(Color.FromRgb(209, 213, 218));
}
