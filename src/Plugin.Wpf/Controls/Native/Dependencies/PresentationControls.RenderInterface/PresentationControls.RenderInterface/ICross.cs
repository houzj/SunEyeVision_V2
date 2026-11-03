using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface ICross : IShape, IShapeBase
{
	Point CenterPoint { get; }
}
