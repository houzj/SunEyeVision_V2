using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;

public class PopupWithTopmost : Popup
{
	public struct RECT
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	public static DependencyProperty TopmostProperty = Window.TopmostProperty.AddOwner(typeof(PopupWithTopmost), new FrameworkPropertyMetadata(false, OnTopmostChanged));

	public static readonly DependencyProperty PopupPlacementTargetProperty = DependencyProperty.RegisterAttached("PopupPlacementTarget", typeof(DependencyObject), typeof(PopupWithTopmost), new PropertyMetadata(null, OnPopupPlacementTargetChanged));

	public bool Topmost
	{
		get
		{
			return (bool)GetValue(TopmostProperty);
		}
		set
		{
			SetValue(TopmostProperty, value);
		}
	}

	public DependencyObject PopupPlacementTarget
	{
		get
		{
			return (DependencyObject)GetValue(PopupPlacementTargetProperty);
		}
		set
		{
			SetValue(PopupPlacementTargetProperty, value);
		}
	}

	public new event EventHandler Opened;

	private static void OnPopupPlacementTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue == null)
		{
			return;
		}
		DependencyObject dependencyObject = e.NewValue as DependencyObject;
		Popup pop = d as Popup;
		Window window = Window.GetWindow(dependencyObject);
		if (window != null)
		{
			window.LocationChanged += delegate
			{
				double horizontalOffset = pop.HorizontalOffset;
				pop.HorizontalOffset = horizontalOffset + 1.0;
				pop.HorizontalOffset = horizontalOffset;
			};
		}
	}

	private static void OnTopmostChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
	{
		(obj as PopupWithTopmost).UpdateWindow();
	}

	protected override void OnOpened(EventArgs e)
	{
		Opened?.Invoke(this, e);
		UpdateWindow();
	}

	private void UpdateWindow()
	{
		IntPtr? intPtr = (PresentationSource.FromVisual(Child) as HwndSource)?.Handle;
		if (intPtr.HasValue && GetWindowRect(intPtr.Value, out var lpRect))
		{
			SetWindowPos(intPtr.Value, Topmost ? (-1) : (-2), lpRect.Left, lpRect.Top, (int)Width, (int)Height, 0);
		}
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	[DllImport("user32")]
	private static extern int SetWindowPos(IntPtr hWnd, int hwndInsertAfter, int x, int y, int cx, int cy, int wFlags);
}
