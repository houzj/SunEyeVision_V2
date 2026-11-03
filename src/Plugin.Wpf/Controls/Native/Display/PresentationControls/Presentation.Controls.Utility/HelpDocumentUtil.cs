using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.View.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.ViewUtility;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Utility;

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
