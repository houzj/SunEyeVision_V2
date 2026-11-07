using System;

namespace VM.Controls.Core;

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
