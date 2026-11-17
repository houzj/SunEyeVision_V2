using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.WorkspaceControls.Base;

/// <summary>
/// 节点连接端口：视觉尺寸、橙色导向条与命中放大行为对齐原件 Connector。
/// </summary>
public partial class ConnectionPort : Control
{
    public static readonly DependencyProperty IsHitedProperty = DependencyProperty.Register(
        nameof(IsHited),
        typeof(bool),
        typeof(ConnectionPort),
        new PropertyMetadata(false));

    public ConnectionPort()
    {
        InitializeComponent();
    }

    /// <summary>端口是否被命中，命中时导向条放大。</summary>
    /// <summary>悬停在端口上时进入命中态（放大），离开即恢复——与原件 Connector 的 IsHited 语义一致。</summary>
    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        IsHited = true;
        e.Handled = true;
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        IsHited = false;
    }

    public bool IsHited
    {
        get => (bool)GetValue(IsHitedProperty);
        set => SetValue(IsHitedProperty, value);
    }

    /// <summary>端口所在方位，与原件连接方位枚举一致。</summary>
    public ConnectorOrientation Orientation { get; set; }

    /// <summary>端口序号：左 0、上 1、右 2、下 3，与原件一致。</summary>
    public int Index { get; set; }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsMouseOverProperty)
        {
            IsHited = (bool)e.NewValue;
        }
    }
}
