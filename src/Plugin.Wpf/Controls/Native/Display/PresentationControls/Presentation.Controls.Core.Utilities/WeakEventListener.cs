using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core.Utilities;

internal class WeakEventListener<TArgs> : IWeakEventListener where TArgs : EventArgs
{
	private Action<object, TArgs> _callback;

	public WeakEventListener(Action<object, TArgs> callback)
	{
		if (callback == null)
		{
			throw new ArgumentNullException("callback");
		}
		_callback = callback;
	}

	public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
	{
		_callback(sender, (TArgs)e);
		return true;
	}
}
