namespace SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

public interface IImageData
{
	int Width { get; }

	int Height { get; }

	byte[] Buffer { get; set; }

	string MemoryAddress { get; }

	string PixelFormat { get; }
}
