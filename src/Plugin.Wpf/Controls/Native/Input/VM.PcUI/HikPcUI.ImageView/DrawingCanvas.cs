using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ImageView;

public class DrawingCanvas : Canvas
{
	private List<Visual> visuals = new List<Visual>();

	protected override int VisualChildrenCount => visuals.Count;

	protected override Visual GetVisualChild(int index)
	{
		return visuals[index];
	}

	public void AddVisual(Visual visual)
	{
		visuals.Add(visual);
		AddVisualChild(visual);
	}

	public void DeleteVisual(Visual visual)
	{
		visuals.Remove(visual);
		RemoveVisualChild(visual);
	}

	public void ClearVisuals()
	{
		foreach (Visual visual in visuals)
		{
			RemoveVisualChild(visual);
		}
		visuals.Clear();
	}
}
