namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IImageData
{
	int Width { get; }

	int Height { get; }

	byte[] Buffer { get; set; }

	string MemoryAddress { get; }

	string PixelFormat { get; }
}
