using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

internal class CachedTextInfo : ICloneable
{
	public string Text { get; private set; }

	public int CaretIndex { get; private set; }

	public int SelectionStart { get; private set; }

	public int SelectionLength { get; private set; }

	private CachedTextInfo(string text, int caretIndex, int selectionStart, int selectionLength)
	{
		Text = text;
		CaretIndex = caretIndex;
		SelectionStart = selectionStart;
		SelectionLength = selectionLength;
	}

	public CachedTextInfo(TextBox textBox)
		: this(textBox.Text, textBox.CaretIndex, textBox.SelectionStart, textBox.SelectionLength)
	{
	}

	public object Clone()
	{
		return new CachedTextInfo(Text, CaretIndex, SelectionStart, SelectionLength);
	}
}
