using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class QueryMoveFocusEventArgs : RoutedEventArgs
{
	private FocusNavigationDirection m_navigationDirection;

	private bool m_reachedMaxLength;

	private bool m_canMove = true;

	public FocusNavigationDirection FocusNavigationDirection => m_navigationDirection;

	public bool ReachedMaxLength => m_reachedMaxLength;

	public bool CanMoveFocus
	{
		get
		{
			return m_canMove;
		}
		set
		{
			m_canMove = value;
		}
	}

	private QueryMoveFocusEventArgs()
	{
	}

	internal QueryMoveFocusEventArgs(FocusNavigationDirection direction, bool reachedMaxLength)
		: base(AutoSelectTextBox.QueryMoveFocusEvent)
	{
		m_navigationDirection = direction;
		m_reachedMaxLength = reachedMaxLength;
	}
}
