using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PlatformStyles.Styles;

public static class InteractionColors
{
	public static readonly Brush InteractionBlue = new SolidColorBrush(Color.FromRgb(63, 133, 228));

	public static readonly Brush InteractionPressBlue = new SolidColorBrush(Color.FromRgb(49, 111, 194));

	public static readonly Brush InteractionHoverBlue = new SolidColorBrush(Color.FromRgb(34, 105, 175));

	public static readonly Brush InteractionFontGray = new SolidColorBrush(Color.FromRgb(154, 154, 154));

	public static readonly Brush InteractionFontDark = new SolidColorBrush(Color.FromRgb(76, 76, 76));

	public static readonly Brush InteractionControlLight = new SolidColorBrush(Color.FromRgb(250, 250, 250));

	public static readonly Brush InteractionControlGray = new SolidColorBrush(Color.FromRgb(209, 213, 218));
}
