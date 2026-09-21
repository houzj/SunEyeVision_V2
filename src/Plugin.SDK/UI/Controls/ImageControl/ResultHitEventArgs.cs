using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Plugin.SDK.UI.Controls;

/// <summary>结果图形命中事件参数。</summary>
public sealed class ResultHitEventArgs : RoutedEventArgs
{
    public ResultHitEventArgs(DisplayItemBase item, Point imagePosition, MouseButtonEventArgs originalEventArgs, RoutedEvent routedEvent)
        : base(routedEvent)
    {
        Item = item;
        ImagePosition = imagePosition;
        OriginalEventArgs = originalEventArgs;
    }

    public DisplayItemBase Item { get; }
    public Point ImagePosition { get; }
    public MouseButtonEventArgs OriginalEventArgs { get; }
    public new bool Handled
    {
        get => OriginalEventArgs.Handled;
        set => OriginalEventArgs.Handled = value;
    }
}
