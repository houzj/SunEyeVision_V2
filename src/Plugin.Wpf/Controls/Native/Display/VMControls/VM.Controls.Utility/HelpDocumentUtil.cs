using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using Apps.Localization;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Utility;

public static class HelpDocumentUtil
{
	public static void OpenHelpDocument(string toolItemInfoPath)
	{
		if (!string.IsNullOrEmpty(toolItemInfoPath))
		{
			string language = ((AppLocalizationService.GetLangType() == "zh-cn") ? "CH" : "EN");
			switch (BrowserUtil.OpenModuleHelpUrl(toolItemInfoPath, language))
			{
			case 0:
				return;
			case 1:
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_本机无法打开网页"));
				return;
			}
		}
		HikMessageBox.Show(AppLocalizationService.GetLang("Lang_模块帮助文档的地址不存在"));
	}
}
