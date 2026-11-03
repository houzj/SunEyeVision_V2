using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IMicroPoint : IShape, IShapeBase
{
	Point Point { get; }
}
