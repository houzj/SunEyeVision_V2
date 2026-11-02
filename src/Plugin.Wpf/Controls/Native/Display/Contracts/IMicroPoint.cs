using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IMicroPoint : IShape, IShapeBase
{
	Point Point { get; }
}
