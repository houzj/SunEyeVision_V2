using System.Collections.Generic;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IPolyline : IShape, IShapeBase
{
	List<Point> PolylinePointList { get; }
}
