using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface ICircle : IShape, IShapeBase
{
	Point CenterPoint { get; }

	double Radius { get; }
}
