using System.Collections.Generic;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IArrowPath : IShape, IShapeBase
{
	List<Point> Points { get; }
}
