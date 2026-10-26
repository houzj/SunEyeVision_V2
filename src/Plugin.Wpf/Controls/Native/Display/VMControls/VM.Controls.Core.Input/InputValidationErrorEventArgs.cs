using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Input;

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
