using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public static class WindowHelper
{
	public struct HWND
	{
		public IntPtr h;

		public static HWND NULL => new HWND
		{
			h = IntPtr.Zero
		};

		public static HWND Cast(IntPtr h)
		{
			return new HWND
			{
				h = h
			};
		}

		public static implicit operator IntPtr(HWND h)
		{
			return h.h;
		}

		public static bool operator ==(HWND hl, HWND hr)
		{
			return hl.h == hr.h;
		}

		public static bool operator !=(HWND hl, HWND hr)
		{
			return hl.h != hr.h;
		}

		public override bool Equals(object oCompare)
		{
			HWND hWND = Cast((HWND)oCompare);
			return h == hWND.h;
		}

		public override int GetHashCode()
		{
			return (int)h;
		}
	}

	internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

	public const uint GW_OWNER = 4u;

	public const uint GW_HWNDPREV = 3u;

	public const uint GW_HWNDNEXT = 2u;

	public const uint GW_HWNDLAST = 1u;

	public const uint GW_HWNDFIRST = 0u;

	public const uint GW_ENABLEDPOPUP = 6u;

	public const uint GW_CHILD = 5u;

	public static IntPtr GetWindowHandler(this Window window)
	{
		if (window.IsInitialized && window.IsLoaded && window.IsVisible)
		{
			return IntPtr.Zero;
		}
		if (PresentationSource.FromVisual(window) is HwndSource hwndSource)
		{
			return hwndSource.Handle;
		}
		return IntPtr.Zero;
	}

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	public static extern bool GetWindowPlacement(HWND hWnd, ref BaseWindow.WINDOWPLACEMENT placement);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern bool SetWindowPlacement(HWND hwnd, ref BaseWindow.WINDOWPLACEMENT wp);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	internal static extern int SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int width, int height, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	internal static extern int UpdateWindow(IntPtr hPtr);

	public static void DoEvents(this Dispatcher dispatcher)
	{
		DispatcherFrame dispatcherFrame = new DispatcherFrame();
		dispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(ExitFrames), dispatcherFrame);
		try
		{
			Dispatcher.PushFrame(dispatcherFrame);
		}
		catch (InvalidOperationException)
		{
		}
	}

	private static object ExitFrames(object frame)
	{
		((DispatcherFrame)frame).Continue = false;
		return null;
	}

	[DllImport("user32.dll", SetLastError = true)]
	public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int ID);

	[DllImport("user32.dll")]
	public static extern int GetWindowTextLength(IntPtr hWnd);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int nMaxCount);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	public static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool BringWindowToTop(IntPtr hWnd);

	[DllImport("user32.dll")]
	public static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	public static extern IntPtr SetActiveWindow(IntPtr HWND);

	public static int GetWindowThreadProcessId(this IntPtr hwnd)
	{
		int ID = 0;
		if (hwnd != IntPtr.Zero)
		{
			GetWindowThreadProcessId(hwnd, out ID);
		}
		return ID;
	}

	public static void SwitchTo(this BaseWindow window)
	{
		if (!window.IsClosing && window.IsVisible)
		{
			SwitchToThisWindow(window.GetWindowHandler(), fAltTab: true);
		}
	}

	public static int GetWindowThreadProcessId(this Window window)
	{
		if (window == null)
		{
			return 0;
		}
		return window.Dispatcher.Invoke(() => (PresentationSource.FromVisual(window) is HwndSource hwndSource) ? hwndSource.Handle.GetWindowThreadProcessId() : 0);
	}

	public static Window GetWindowFromHwnd(this IntPtr hwnd)
	{
		if (hwnd == IntPtr.Zero)
		{
			return null;
		}
		HwndSource hwndSource = HwndSource.FromHwnd(hwnd);
		if (hwndSource != null)
		{
			return hwndSource.RootVisual as Window;
		}
		return null;
	}

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	internal static extern int GetWindowThreadProcessId(IntPtr hWnd, out IntPtr lpdwProcessId);

	[DllImport("User32.dll", CharSet = CharSet.Auto)]
	internal static extern bool IsWindowVisible(IntPtr hWnd);

	public static IntPtr GetMainWindowHandle(int processId)
	{
		IntPtr mainWindowHandle = IntPtr.Zero;
		EnumWindows((IntPtr hWnd, IntPtr lParam) =>
		{
			GetWindowThreadProcessId(hWnd, out IntPtr lpdwProcessId);
			if (lpdwProcessId == lParam && IsWindowVisible(hWnd) && GetWindow(hWnd, 4u) == IntPtr.Zero)
			{
				mainWindowHandle = hWnd;
				return false;
			}
			return true;
		}, new IntPtr(processId));
		return mainWindowHandle;
	}
}
