using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface ILine : IShape, IShapeBase
{
	Point StartPoint { get; }

	Point EndPoint { get; }

	bool IsThroughLine { get; }
}
