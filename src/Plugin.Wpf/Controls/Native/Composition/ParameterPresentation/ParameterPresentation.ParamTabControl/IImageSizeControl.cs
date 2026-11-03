using SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.ParameterPresentation.ParamTabControl;
namespace SunEyeVision.Plugin.Wpf.Native.Composition.ParameterPresentation.ParamTabControl;

public interface IImageSizeControl
{
	int ImageWidth { get; set; }

	int ImageHeight { get; set; }
}
