using System;
using System.Windows.Controls;
using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Shell.Controls.Shell;

/// <summary>延迟创建 VM 结果宿主，避免启动壳提前创建图像/结果树和资源会话。</summary>
public sealed class DeferredImageResultHostControl : ContentControl
{
    private ImageResultControl? _inner;

    public bool IsDeferredCreated => _inner is not null;

    public void Initialize(MainWindowViewModel viewModel)
    {
        if (_inner is null)
        {
            _inner = new ImageResultControl();
            Content = _inner;
        }

        _inner.Initialize(viewModel);
    }

    public void ToggleFullscreen()
    {
        if (_inner is null)
            throw new InvalidOperationException("结果宿主尚未完成组装。");
        _inner.ToggleFullscreen();
    }

    public void Shutdown() => _inner?.Shutdown();
}
