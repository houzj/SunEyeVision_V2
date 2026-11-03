using SunEyeVision.Plugin.Wpf.Native.Dependencies.View.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.ViewUtility;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ViewUtility;

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
