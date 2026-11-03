using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Coordinates;
namespace SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;

public interface IImageSizeControl
{
	int ImageWidth { get; set; }

	int ImageHeight { get; set; }
}
