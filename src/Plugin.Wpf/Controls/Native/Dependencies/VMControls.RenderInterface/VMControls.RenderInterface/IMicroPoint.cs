using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IMicroPoint : IShape, IShapeBase
{
	Point Point { get; }
}
