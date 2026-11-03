using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core;

public class QueryValueFromTextEventArgs : EventArgs
{
	private string m_text;

	private object m_value;

	private bool m_hasParsingError;

	public string Text => m_text;

	public object Value
	{
		get
		{
			return m_value;
		}
		set
		{
			m_value = value;
		}
	}

	public bool HasParsingError
	{
		get
		{
			return m_hasParsingError;
		}
		set
		{
			m_hasParsingError = value;
		}
	}

	public QueryValueFromTextEventArgs(string text, object value)
	{
		m_text = text;
		m_value = value;
	}
}
