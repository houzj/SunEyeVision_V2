using System;

namespace VM.Controls.Core;

public class InvalidTemplateException : Exception
{
	public InvalidTemplateException(string message)
		: base(message)
	{
	}

	public InvalidTemplateException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
