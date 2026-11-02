using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IText : IShapeBase
{
	string Content { get; }

	Point Position { get; }

	int FontSize { get; }
}
