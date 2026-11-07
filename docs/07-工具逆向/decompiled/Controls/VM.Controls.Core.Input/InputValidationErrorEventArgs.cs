using System;

namespace VM.Controls.Core.Input;

public class InputValidationErrorEventArgs : EventArgs
{
	private Exception exception;

	private bool _throwException;

	public Exception Exception
	{
		get
		{
			return exception;
		}
		private set
		{
			exception = value;
		}
	}

	public bool ThrowException
	{
		get
		{
			return _throwException;
		}
		set
		{
			_throwException = value;
		}
	}

	public InputValidationErrorEventArgs(Exception e)
	{
		Exception = e;
	}
}
