using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;

public interface IImageSizeControl
{
	int ImageWidth { get; set; }

	int ImageHeight { get; set; }
}
