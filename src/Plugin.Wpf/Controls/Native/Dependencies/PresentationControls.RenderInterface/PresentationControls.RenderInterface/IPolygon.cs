using System.Collections.Generic;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IPolygon : IShape, IShapeBase
{
	List<Point> Points { get; }
}
