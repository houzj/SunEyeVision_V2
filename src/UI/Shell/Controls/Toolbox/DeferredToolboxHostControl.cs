using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.UI.Shell.Controls.Toolbox;

/// <summary>延迟创建工具箱，准入完成前只保留布局槽位，不枚举工具项或创建图标树。</summary>
public sealed class DeferredToolboxHostControl : ContentControl
{
    private ToolboxControl? _inner;

    public bool IsDeferredCreated => _inner is not null;

    public DeferredToolboxHostControl()
    {
        DataContextChanged += (_, e) =>
        {
            if (_inner is not null)
                _inner.DataContext = e.NewValue;
        };
    }

    public void Initialize()
    {
        if (_inner is not null)
            return;

        _inner = new ToolboxControl { DataContext = DataContext };
        Content = _inner;
        _inner.Visibility = Visibility.Visible;
    }

}
