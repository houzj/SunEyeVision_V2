namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IShape : IShapeBase
{
	string Fill { get; }

	double StrokeThickness { get; }

	string ToolTip { get; }
}
