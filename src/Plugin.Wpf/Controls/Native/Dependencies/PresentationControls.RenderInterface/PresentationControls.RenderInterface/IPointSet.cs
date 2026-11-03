using System.Collections.Generic;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IPointSet : IShape, IShapeBase
{
	int PointNum { get; }

	List<Point> PointList { get; }
}
