using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IPoint : IShape, IShapeBase
{
	Point Point { get; }
}
