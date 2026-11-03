using SunEyeVision.Plugin.Wpf.Native.Display.Apps.ColorFun;
using SunEyeVision.Plugin.Wpf.Themes.Palettes;
using SunEyeVision.Plugin.Wpf.Infrastructure.Resources;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.ValueConverters;

namespace SunEyeVision.Plugin.Wpf.Themes.Palettes;

/// <summary>迁入原颜色资源的窗口级宿主适配；不依赖VM安装目录或注入Application资源。</summary>
public class ColorThemesResourceDictionary : DynamicResourceDictionary
{
    private const string ResourceRoot = "/SunEyeVision.Plugin.Wpf;component/Themes/Palettes/";
    private static readonly ConditionalWeakTable<Dispatcher, ColorThemesResourceDictionary> Instances = new();

    public static ColorThemesResourceDictionary Instance => GetInstance();

    public static ColorThemesResourceDictionary GetInstance(FrameworkElement? framework = null)
    {
        var dispatcher = framework?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.VerifyAccess();
        var instance = Instances.GetValue(dispatcher, _ => new ColorThemesResourceDictionary());
        if (framework != null)
        {
            if (!framework.Resources.MergedDictionaries.Contains(instance))
                framework.Resources.MergedDictionaries.Add(instance);
        }
        return instance;
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
        try { GetInstance().LoadTheme(colorStyle); }
        catch (Exception ex) { Trace.TraceError("加载迁入颜色资源失败：{0}", ex); }
    }
}
