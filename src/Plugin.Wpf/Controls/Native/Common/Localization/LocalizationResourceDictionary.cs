using System;
using System.Globalization;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Common.Localization;

/// <summary>插件窗口内的语言资源；不写入宿主全局资源或厂商语言配置。</summary>
public sealed class LocalizationResourceDictionary : ResourceDictionary
{
    public static LocalizationResourceDictionary Instance { get; } = new();

    public string CultureName { get; }

    private LocalizationResourceDictionary()
    {
        CultureName = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh-cn" : "en-us";
        Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Localization/" + CultureName + ".xaml", UriKind.Relative);
    }
}
