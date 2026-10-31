using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Common.Localization;

/// <summary>A XAML-typed dictionary entry that shares the existing language resources without a collection markup extension.</summary>
public sealed class LocalizationResources : ResourceDictionary
{
    public LocalizationResources() => MergedDictionaries.Add(LocalizationResourceDictionary.Instance);
}
