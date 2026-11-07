using System;

namespace VM.Controls;

public delegate long WNDPROC(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
