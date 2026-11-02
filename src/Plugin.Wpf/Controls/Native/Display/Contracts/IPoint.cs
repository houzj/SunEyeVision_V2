using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IPoint : IShape, IShapeBase
{
	Point Point { get; }
}
