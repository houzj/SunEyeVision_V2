using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Adapters.Resources;

/// <summary>
/// 原件颜色键只解析一份。控件通过资源查找继承同一份字典，不再每个控件各自实例化 BAML——
/// 这是参数页首帧的主要成本来源（控件多时成本线性叠加）。
/// </summary>
public static class OriginalThemeResources
{
    private const string Root = "/SunEyeVision.Plugin.Wpf;component/";
    private static readonly Lazy<ResourceDictionary> Original = new(
        () => new ResourceDictionary { Source = new Uri(Root + "Themes/Native/OriginalTheme.xaml", UriKind.Relative) },
        LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Dictionary<string, ResourceDictionary> NativeInputs = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    /// <summary>原件颜色键，进程内唯一实例。</summary>
    public static ResourceDictionary Shared => Original.Value;

    /// <summary>按相对路径取原生输入控件字典，同名只解析一次。</summary>
    public static ResourceDictionary NativeInput(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (Gate)
        {
            if (!NativeInputs.TryGetValue(name, out var dictionary))
            {
                dictionary = new ResourceDictionary { Source = new Uri(Root + name, UriKind.Relative) };
                NativeInputs.Add(name, dictionary);
            }
            return dictionary;
        }
    }
}
