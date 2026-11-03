using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

public interface IText : IShapeBase
{
	string Content { get; }

	Point Position { get; }

	int FontSize { get; }
}
