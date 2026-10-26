using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IAngleShape : IShape, IShapeBase
{
	Point InitialLineStartPoint { get; }

	Point InitialLineEndPoint { get; }

	Point TerminalLineStartPoint { get; }

	Point TerminalLineEndPoint { get; }

	Point Intersection { get; }

	double Angle { get; }

	IAngleShape GetAngleShape(Point line1Point1, Point line1Point2, Point line2Point1, Point line2Point2, Point intersection, double angle);
}
