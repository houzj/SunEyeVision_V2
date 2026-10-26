namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IShape : IShapeBase
{
	string Fill { get; }

	double StrokeThickness { get; }

	string ToolTip { get; }
}
