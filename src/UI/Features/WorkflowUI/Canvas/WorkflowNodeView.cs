using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;

/// <summary>
/// 节点视图宿主：对应原件 DiagramItem，由控件自身维护命中状态。
/// 端口显示条件取自 <see cref="IsHited"/>，不再依赖模板内 ElementName 绑定或宿主几何扫描。
/// </summary>
public class WorkflowNodeView : ContentControl
{
    public static readonly DependencyProperty IsHitedProperty = DependencyProperty.Register(
        nameof(IsHited),
        typeof(bool),
        typeof(WorkflowNodeView),
        new PropertyMetadata(false));

    /// <summary>节点是否被鼠标命中；命中时四向端口显示，与原件 DiagramItem.IsHited 同名同义。</summary>
    public bool IsHited
    {
        get => (bool)GetValue(IsHitedProperty);
        set => SetValue(IsHitedProperty, value);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty)
        {
            IsHited = (bool)e.NewValue;
        }
    }
}
