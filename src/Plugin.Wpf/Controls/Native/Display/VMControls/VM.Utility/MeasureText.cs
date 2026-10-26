using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;

public class MeasureText
{
	public double Width { get; private set; }

	public double Height { get; private set; }

	public MeasureText(string text, double fontSize, string fontFamily)
	{
		FormattedText formattedText = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(fontFamily), fontSize, Brushes.Black);
		Width = formattedText.WidthIncludingTrailingWhitespace;
		Height = formattedText.Height;
	}
}
