using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class ShapeCanvas : Canvas
{
	public static readonly DependencyProperty ToolTipContentProperty;

	private List<Visual> visuals = new List<Visual>();

	private List<string> types = new List<string>();

	public string ToolTipContent
	{
		get
		{
			return (string)GetValue(ToolTipContentProperty);
		}
		set
		{
			SetValue(ToolTipContentProperty, value);
		}
	}

	protected override int VisualChildrenCount => visuals.Count;

	protected override Visual GetVisualChild(int index)
	{
		return visuals[index];
	}

	internal int GetNumByType(string type)
	{
		return types.Count((string p) => p == type);
	}

	static ShapeCanvas()
	{
		ToolTipContentProperty = DependencyProperty.Register("ToolTipContent", typeof(string), typeof(ShapeCanvas), new PropertyMetadata(null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ShapeCanvas), new FrameworkPropertyMetadata(typeof(ShapeCanvas)));
	}

	public ShapeCanvas()
	{
		MouseMove += ShapeCanvas_MouseMove;
	}

	private void ShapeCanvas_MouseMove(object sender, MouseEventArgs e)
	{
		Point position = e.GetPosition((UIElement)sender);
		VisualTreeHelper.HitTest(this, null, MyCallback, new PointHitTestParameters(position));
	}

	public HitTestResultBehavior MyCallback(HitTestResult result)
	{
		if (result.VisualHit.GetType() == typeof(TagDrawingVisual))
		{
			object tag = (result.VisualHit as TagDrawingVisual).Tag;
			if (tag != null && !string.IsNullOrEmpty(tag.ToString()))
			{
				ToolTipContent = tag.ToString();
			}
			else
			{
				ToolTipContent = null;
			}
		}
		return HitTestResultBehavior.Stop;
	}

	public void AddVisual(Visual visual, string type)
	{
		visuals.Add(visual);
		types.Add(type);
		AddVisualChild(visual);
	}

	public void DeleteVisual(Visual visual)
	{
		int num = visuals.IndexOf(visual);
		visuals.Remove(visual);
		if (num >= 0 && num < types.Count)
		{
			types.RemoveAt(num);
		}
		RemoveVisualChild(visual);
	}

	public void ClearVisuals()
	{
		foreach (Visual visual in visuals)
		{
			RemoveVisualChild(visual);
		}
		visuals.Clear();
		types.Clear();
	}
}
