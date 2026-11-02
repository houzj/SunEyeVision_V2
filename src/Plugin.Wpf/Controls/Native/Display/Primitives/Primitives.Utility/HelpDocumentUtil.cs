using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Platform;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Utility;

public static class HelpDocumentUtil
{
	public static void OpenHelpDocument(string toolItemInfoPath)
	{
		if (!string.IsNullOrEmpty(toolItemInfoPath))
		{
			string language = ((AppLocalizationService.GetLangType() == "zh-cn") ? "CH" : "EN");
			switch (SunEyeVision.Plugin.Wpf.Native.Common.NativeHelpDocument.OpenModuleHelpUrl(toolItemInfoPath, language))
			{
			case 0:
				return;
			case 1:
				NativeMessageBox.Show(AppLocalizationService.GetLang("Lang_本机无法打开网页"));
				return;
			}
		}
		NativeMessageBox.Show(AppLocalizationService.GetLang("Lang_模块帮助文档的地址不存在"));
	}
}
