using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace VM.Utility;

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
