using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IAnnularSector : IShape, IShapeBase
{
	Point CenterPoint { get; }

	double OuterRadius { get; }

	double InnerRadius { get; }

	double StartAngle { get; }

	double AngleRange { get; }
}
