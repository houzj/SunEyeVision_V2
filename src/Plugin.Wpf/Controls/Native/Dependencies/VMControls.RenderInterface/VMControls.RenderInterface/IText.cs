using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IText : IShapeBase
{
	string Content { get; }

	Point Position { get; }

	int FontSize { get; }
}
