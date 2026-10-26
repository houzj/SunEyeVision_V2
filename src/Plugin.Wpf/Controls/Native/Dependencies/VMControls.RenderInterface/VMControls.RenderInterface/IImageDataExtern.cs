using System.Collections.Generic;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

public interface IImageDataExtern
{
	bool NeedConvertColor { get; }

	string PixelInfoLabel { get; }

	Dictionary<string, string> PixelExtraInfo { get; }

	bool NeedCheckExecuteCount { get; }

	uint ExecuteCount { get; }

	void SetPixelFormat(string format);
}
