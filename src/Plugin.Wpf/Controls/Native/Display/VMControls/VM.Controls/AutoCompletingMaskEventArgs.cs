using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class AutoCompletingMaskEventArgs : CancelEventArgs
{
	private MaskedTextProvider m_maskedTextProvider;

	private int m_startPosition;

	private int m_selectionLength;

	private string m_input;

	private int m_autoCompleteStartPosition;

	private string m_autoCompleteText;

	public MaskedTextProvider MaskedTextProvider => m_maskedTextProvider;

	public int StartPosition => m_startPosition;

	public int SelectionLength => m_selectionLength;

	public string Input => m_input;

	public int AutoCompleteStartPosition
	{
		get
		{
			return m_autoCompleteStartPosition;
		}
		set
		{
			m_autoCompleteStartPosition = value;
		}
	}

	public string AutoCompleteText
	{
		get
		{
			return m_autoCompleteText;
		}
		set
		{
			m_autoCompleteText = value;
		}
	}

	public AutoCompletingMaskEventArgs(MaskedTextProvider maskedTextProvider, int startPosition, int selectionLength, string input)
	{
		m_autoCompleteStartPosition = -1;
		m_maskedTextProvider = maskedTextProvider;
		m_startPosition = startPosition;
		m_selectionLength = selectionLength;
		m_input = input;
	}
}
