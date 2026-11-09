using System;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.UI.Features.WorkflowUI.Canvas;
using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Shell.Controls.Shell;

/// <summary>
/// VM 风格的工作区延迟宿主。窗口壳先可见，准入完成后才创建真正的 Tab/画布树。
/// 该类只负责视觉宿主，不改变 ShellWorkspaceControl 的编辑和释放语义。
/// </summary>
public sealed class DeferredWorkspaceHostControl : ContentControl
{
    private ShellWorkspaceControl? _inner;

    public event Action<double>? ZoomScaleChanged;

    public bool IsDeferredCreated => _inner is not null;

    public void Initialize(MainWindowViewModel viewModel)
    {
        if (_inner is null)
        {
            _inner = new ShellWorkspaceControl();
            _inner.ZoomScaleChanged += OnZoomScaleChanged;
            Content = _inner;
        }

        _inner.Initialize(viewModel);
    }

    public WorkflowCanvasControl? GetCurrentWorkflowCanvas() => _inner?.GetCurrentWorkflowCanvas();

    public void OpenSubroutineNodeParameterDialog(CanvasNodeProjection node)
    {
        if (_inner is null)
            throw new InvalidOperationException("工作区尚未完成组装。");
        _inner.OpenSubroutineNodeParameterDialog(node);
    }

    public void SwitchCanvasType(CanvasType canvasType)
    {
        if (_inner is null)
            throw new InvalidOperationException("工作区尚未完成组装。");
        _inner.SwitchCanvasType(canvasType);
    }

    public void SetPathCalculator(string pathCalculatorType)
    {
        if (_inner is null)
            throw new InvalidOperationException("工作区尚未完成组装。");
        _inner.SetPathCalculator(pathCalculatorType);
    }

    public CanvasType GetCurrentCanvasType() => _inner?.GetCurrentCanvasType() ?? CanvasType.WorkflowCanvas;

    private void OnZoomScaleChanged(double value) => ZoomScaleChanged?.Invoke(value);
}
