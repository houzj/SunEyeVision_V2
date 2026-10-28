using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Display;

namespace SunEyeVision.Plugin.Wpf.Design;

public partial class NativeCrosshairPreview : UserControl, IDisposable
{
    public NativeCrosshairPreview() => InitializeComponent();
    public void Dispose() { var child = Content; Content = null; (child as IDisposable)?.Dispose(); }
}

/// <summary>Original config composition and renderer, without host/Runtime/algorithm.</summary>
public sealed class NativeCrosshairSample : Decorator, IDisposable
{
    private readonly NativeRoiSamples.Image _image = new();
    private bool _disposed;
    public NativeResultViewport Viewport { get; } = new();
    public NativeResultViewport.CrosshairLayer Crosshairs { get; }
    public CrosshairSettingsView Settings { get; }
    public NativeCrosshairSample()
    {
        Crosshairs = Viewport.CreateCrosshairLayer();
        try
        {
            if (!Viewport.BindImage(_image, () => !_disposed)) throw new InvalidOperationException("独立辅助线图像未接纳。");
            Crosshairs.Activate();
            Settings = new() { DataContext = Crosshairs, Width = 380 };
            var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(380) }); grid.ColumnDefinitions.Add(new());
            var pane = new DockPanel(); var open = new Button { Content = "十字辅助线", Height = 28 };
            open.Click += (_, _) => { if (Window.GetWindow(this) is {} owner) Crosshairs.OpenSettings(owner); };
            DockPanel.SetDock(open, Dock.Bottom); pane.Children.Add(open); pane.Children.Add(Settings);
            grid.Children.Add(pane); Grid.SetColumn(Viewport.View, 1); grid.Children.Add(Viewport.View); Child = grid;
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        try { Viewport.Dispose(); } finally { try { Settings?.ReleaseBindings(); } finally { _image.Dispose(); Child = null; } }
    }
}
