using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IRectangle : IShape, IShapeBase
{
	Point CenterPoint { get; }

	double Width { get; }

	double Height { get; }

	double Angle { get; }

	double SkewAngleX { get; }

	double SkewAngleY { get; }
}
