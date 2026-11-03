using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IPoint : IShape, IShapeBase
{
	Point Point { get; }
}
