using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface ICoordinateShape : IShape, IShapeBase
{
	Point CenterPoint { get; }

	Point VectorX { get; }

	Point VectorY { get; }
}
