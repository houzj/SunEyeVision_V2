using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface ICircle : IShape, IShapeBase
{
	Point CenterPoint { get; }

	double Radius { get; }
}
