using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Adapters.Resources;

/// <summary>Original color keys only; templates stay local to their native control family.</summary>
public static class OriginalThemeResources
{
    public static ResourceDictionary Create() => new()
    {
        Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/OriginalTheme.xaml", UriKind.Relative)
    };
}
