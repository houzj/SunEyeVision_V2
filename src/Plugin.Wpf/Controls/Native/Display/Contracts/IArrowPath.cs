using System.Collections.Generic;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IArrowPath : IShape, IShapeBase
{
	List<Point> Points { get; }
}
