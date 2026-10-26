using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IEllipse : IShape, IShapeBase
{
	Point CenterPoint { get; }

	double MajorRadius { get; }

	double MinorRadius { get; }

	double Angle { get; }
}
