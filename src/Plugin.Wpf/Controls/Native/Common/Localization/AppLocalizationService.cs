using System.Collections.Generic;

namespace SunEyeVision.Plugin.Wpf.Native.Common.Localization;

/// <summary>迁入视觉控件的查询边界；不承接VM安装路径、业务模型或全局配置写入。</summary>
public static class AppLocalizationService
{
    public static IReadOnlyDictionary<string, string>? InternalResources { get; set; }

    public static string? GetLang(string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (key.Length > 1 && key[0] == '%' && key[^1] == '%') return key[1..^1];
        var dictionary = LocalizationResourceDictionary.Instance;
        return dictionary[key] as string ?? key;
    }

    public static string GetLang(string? key, string fallback)
    {
        var dictionary = LocalizationResourceDictionary.Instance;
        return !string.IsNullOrEmpty(key) && dictionary.Contains(key) && dictionary[key] is string text ? text : fallback;
    }

    public static string? GetInternalLang(string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (key.Length > 1 && key[0] == '%' && key[^1] == '%') return key[1..^1];
        return InternalResources != null && InternalResources.TryGetValue(key, out var text) ? text : key;
    }

    public static string GetLangType() => LocalizationResourceDictionary.Instance.CultureName;
}
