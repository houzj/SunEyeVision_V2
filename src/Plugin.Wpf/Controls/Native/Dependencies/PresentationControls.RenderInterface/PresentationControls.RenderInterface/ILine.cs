using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface ILine : IShape, IShapeBase
{
	Point StartPoint { get; }

	Point EndPoint { get; }

	bool IsThroughLine { get; }
}
