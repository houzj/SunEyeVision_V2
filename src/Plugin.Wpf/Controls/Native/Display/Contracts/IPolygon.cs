using System.Collections.Generic;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IPolygon : IShape, IShapeBase
{
	List<Point> Points { get; }
}
