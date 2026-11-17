using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;

namespace SunEyeVision.UI.Converters.Node;

/// <summary>
/// 画布节点图标解析。原厂每个工具有两张图：
/// 正常态用彩色 <c>_NormalLogo</c>，进入状态态（运行中/成功/失败/跳过）时改用白色 <c>_StateLogo</c>，
/// 因为此时图标底色已经变成状态色块，彩色图标会糊在一起。
/// </summary>
public sealed class ToolNodeIconConverter : IValueConverter, IMultiValueConverter
{
    private const string ResourcePrefix = "pack://application:,,,/SunEyeVision.UI;component/Resources/Toolbox/tool-";
    private const string FallbackKey = "generic";

    /// <summary>已确认不存在的图标键，避免每帧都去探测 pack 资源。</summary>
    private static readonly HashSet<string> Missing = new(StringComparer.Ordinal);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => ResolveFor(value, status: null) ?? DependencyProperty.UnsetValue;

    /// <summary>values: [0]=节点投影, [1]=状态文本（成功/失败/运行中/跳过）。</summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var node = values is { Length: > 0 } ? values[0] : null;
        var status = values is { Length: > 1 } ? values[1] as string : null;
        return ResolveFor(node, status) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static string? ResolveFor(object? value, string? status)
    {
        if (value is not ICanvasToolIconSource node || node.ToolIconKey is not { Length: > 0 } key)
            return Resolve(FallbackKey);
        // 状态态优先用白色图标；取不到再退回彩色图标，最后退回通用图标。
        if (HasStateColor(status))
        {
            var stateIcon = Resolve(key + "-state");
            if (stateIcon is not null) return stateIcon;
        }
        return Resolve(key) ?? Resolve(FallbackKey);
    }

    private static bool HasStateColor(string? status)
        => status is "运行中" or "成功" or "OK" or "失败" or "NG" or "跳过";

    private static string? Resolve(string key)
    {
        if (Missing.Contains(key)) return null;
        var uri = ResourcePrefix + key + ".png";
        try
        {
            _ = System.Windows.Application.GetResourceStream(new Uri(uri, UriKind.Absolute));
            return uri;
        }
        catch (Exception)
        {
            Missing.Add(key);
            return null;
        }
    }
}
