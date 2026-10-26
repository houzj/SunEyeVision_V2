namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IRangeImageData
{
	byte[] RangeBuffer { get; set; }

	uint DetailedPixelFormat { get; set; }

	int Xoffset { get; set; }

	int Yoffset { get; set; }

	int Zoffset { get; set; }

	int Xscale { get; set; }

	int Yscale { get; set; }

	int Zscale { get; set; }
}
