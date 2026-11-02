using System.Collections.Generic;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IPolyline : IShape, IShapeBase
{
	List<Point> PolylinePointList { get; }
}
