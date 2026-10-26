using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ImageView;

internal class BitmapCursor : SafeHandle
{
	public override bool IsInvalid => handle == (IntPtr)(-1);

	public static Cursor CreateBmpCursor(Bitmap cursorBitmap)
	{
		BitmapCursor cursorHandle = new BitmapCursor(cursorBitmap);
		return CursorInteropHelper.Create(cursorHandle);
	}

	protected BitmapCursor(Bitmap cursorBitmap)
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
