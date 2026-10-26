using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

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
