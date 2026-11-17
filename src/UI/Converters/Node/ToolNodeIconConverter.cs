using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;

namespace SunEyeVision.UI.Converters.Node;

/// <summary>Canvas and toolbox consume the same admitted tool declaration.
/// Resolving an icon must not load a plugin or revive an archived tool catalog.</summary>
public sealed class ToolNodeIconConverter : IValueConverter
{
    private const string ResourcePrefix = "pack://application:,,,/SunEyeVision.UI;component/Resources/Toolbox/tool-";
    private const string FallbackKey = "generic";

    /// <summary>已确认不存在的图标键，避免每帧都去探测 pack 资源。</summary>
    private static readonly HashSet<string> Missing = new(StringComparer.Ordinal);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if(value is not ICanvasToolIconSource node || node.ToolIconKey is not { Length: > 0 } key)
            return Resolve(FallbackKey);
        // 工具声明的 IconKey 可能尚无对应位图资源（新增工具常见）：
        // 这时退回通用图标，绝不能渲染成空白——节点缺图标会被误读成"状态异常"。
        return Resolve(key) ?? Resolve(FallbackKey) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)
        =>throw new NotSupportedException();

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
