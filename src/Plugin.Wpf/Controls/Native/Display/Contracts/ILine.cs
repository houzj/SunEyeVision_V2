using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface ILine : IShape, IShapeBase
{
	Point StartPoint { get; }

	Point EndPoint { get; }

	bool IsThroughLine { get; }
}
