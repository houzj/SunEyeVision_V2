using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base;

internal class BitmapCursor : SafeHandle
{
	private static Cursor oldcCursor = null;

	public override bool IsInvalid => handle == (IntPtr)(-1);

	public static Cursor CreateBmpCursor(Bitmap cursorBitmap)
	{
		oldcCursor?.Dispose();
		oldcCursor = null;
		BitmapCursor cursorHandle = new BitmapCursor(cursorBitmap);
		return oldcCursor = CursorInteropHelper.Create(cursorHandle);
	}

	public BitmapCursor(Bitmap cursorBitmap)
		: base((IntPtr)(-1), ownsHandle: true)
	{
		handle = cursorBitmap.GetHicon();
	}

	protected override bool ReleaseHandle()
	{
		bool result = DestroyIcon(handle);
		handle = (IntPtr)(-1);
		return result;
	}

	[DllImport("user32")]
	private static extern bool DestroyIcon(IntPtr hIcon);
}
