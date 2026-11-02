using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface ICross : IShape, IShapeBase
{
	Point CenterPoint { get; }
}
