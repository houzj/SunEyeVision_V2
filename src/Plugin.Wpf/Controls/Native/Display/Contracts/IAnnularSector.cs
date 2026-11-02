using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IAnnularSector : IShape, IShapeBase
{
	Point CenterPoint { get; }

	double OuterRadius { get; }

	double InnerRadius { get; }

	double StartAngle { get; }

	double AngleRange { get; }
}
