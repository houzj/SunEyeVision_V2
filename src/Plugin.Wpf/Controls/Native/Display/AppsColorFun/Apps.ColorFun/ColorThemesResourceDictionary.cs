using System;
using System.Diagnostics;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.BaseData;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Apps.ColorFun;

/// <summary>迁入原颜色资源的窗口级宿主适配；不依赖VM安装目录或注入Application资源。</summary>
public class ColorThemesResourceDictionary : DynamicResourceDictionary
{
    private const string ResourceRoot = "/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/AppsColorFun/";
    private static readonly object Sync = new();
    private static ColorThemesResourceDictionary? instance;

    public static ColorThemesResourceDictionary Instance => GetInstance();

    public static ColorThemesResourceDictionary GetInstance(FrameworkElement? framework = null)
    {
        lock (Sync)
        {
            instance ??= new ColorThemesResourceDictionary();
            if (framework != null)
            {
                framework.Dispatcher.VerifyAccess();
                if (!framework.Resources.MergedDictionaries.Contains(instance))
                    framework.Resources.MergedDictionaries.Add(instance);
            }
            return instance;
        }
    }

    public ColorThemesResourceDictionary() => LoadTheme("colorstyle1");

    private void LoadTheme(string colorStyle)
    {
        var name = colorStyle.ToLowerInvariant();
        if (name is not ("colorstyle1" or "colorstyle2" or "colorstyle3"))
            throw new ArgumentOutOfRangeException(nameof(colorStyle), colorStyle, "仅支持已迁入的三个主题资源。");

        // 先完整加载，失败时保留已显示主题；基础色键覆盖主题中的同名键，沿用原合并顺序。
        var theme = new ResourceDictionary { Source = new Uri(ResourceRoot + name + ".xaml", UriKind.Relative) };
        var common = new ResourceDictionary { Source = new Uri(ResourceRoot + "colorstyle.xaml", UriKind.Relative) };
        MergedDictionaries.Clear();
        MergedDictionaries.Add(theme);
        MergedDictionaries.Add(common);
    }

    public static void SetColorResource(string colorStyle)
    {
        lock (Sync)
        {
            try { GetInstance().LoadTheme(colorStyle); }
            catch (Exception ex) { Trace.TraceError("加载迁入颜色资源失败：{0}", ex); }
        }
    }
}
