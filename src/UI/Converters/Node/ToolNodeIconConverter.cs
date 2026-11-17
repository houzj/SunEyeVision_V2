using System;
using System.Globalization;
using System.Windows.Data;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;

namespace SunEyeVision.UI.Converters.Node;

/// <summary>
/// 画布节点图标解析。原厂每个工具有两张图：
/// 正常态用彩色 <c>_NormalLogo</c>，进入状态态（运行中/成功/失败/跳过）时改用白色 <c>_StateLogo</c>，
/// 因为此时节点上已有状态色，彩色图标会糊在一起。
/// <para>ConverterParameter = "state" 时返回白色状态图，否则返回彩色正常图。</para>
/// <para>这里直接给出 pack URI，不做资源探测——探测失败会被缓存成"缺失"，导致图标永久空白。</para>
/// </summary>
public sealed class ToolNodeIconConverter : IValueConverter
{
    private const string ResourcePrefix = "pack://application:,,,/SunEyeVision.UI;component/Resources/Toolbox/tool-";
    private const string FallbackKey = "generic";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is ICanvasToolIconSource node && node.ToolIconKey is { Length: > 0 } declared
            ? declared
            : FallbackKey;
        var state = string.Equals(parameter as string, "state", StringComparison.Ordinal);
        return ResourcePrefix + (state ? key + "-state" : key) + ".png";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
