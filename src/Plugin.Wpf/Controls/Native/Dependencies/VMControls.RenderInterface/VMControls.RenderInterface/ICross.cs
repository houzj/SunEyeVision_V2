using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface ICross : IShape, IShapeBase
{
	Point CenterPoint { get; }
}
