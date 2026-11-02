using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface ICoordinateShape : IShape, IShapeBase
{
	Point CenterPoint { get; }

	Point VectorX { get; }

	Point VectorY { get; }
}
