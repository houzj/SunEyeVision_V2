using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core;

public class QueryTextFromValueEventArgs : EventArgs
{
	private object m_value;

	private string m_text;

	public object Value => m_value;

	public string Text
	{
		get
		{
			return m_text;
		}
		set
		{
			m_text = value;
		}
	}

	public QueryTextFromValueEventArgs(object value, string text)
	{
		m_value = value;
		m_text = text;
	}
}
