using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Base;

public class RectDragPanalBase : Control
{
	public static readonly DependencyProperty AreaProperty;

	/// <summary>
	/// 位置范围
	/// </summary>
	public Rect Area
	{
		get
		{
			return (Rect)GetValue(AreaProperty);
		}
		set
		{
			SetValue(AreaProperty, value);
		}
	}

	private static void OnAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		RectDragPanalBase rectDragPanalBase = (RectDragPanalBase)d;
		Rect rect = (Rect)e.OldValue;
		Rect rect2 = (Rect)e.NewValue;
		rectDragPanalBase.Width = rect2.Width;
		rectDragPanalBase.Height = rect2.Height;
		Canvas.SetLeft(rectDragPanalBase, rect2.X);
		Canvas.SetTop(rectDragPanalBase, rect2.Y);
	}

	static RectDragPanalBase()
	{
		AreaProperty = DependencyProperty.Register("Area", typeof(Rect), typeof(RectDragPanalBase), new PropertyMetadata(default(Rect), OnAreaChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(RectDragPanalBase), new FrameworkPropertyMetadata(typeof(RectDragPanalBase)));
	}
}
