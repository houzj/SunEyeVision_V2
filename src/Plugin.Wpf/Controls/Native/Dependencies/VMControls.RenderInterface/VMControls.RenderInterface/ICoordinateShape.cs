using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface ICoordinateShape : IShape, IShapeBase
{
	Point CenterPoint { get; }

	Point VectorX { get; }

	Point VectorY { get; }
}
