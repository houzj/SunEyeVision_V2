using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Display;

namespace SunEyeVision.Plugin.Wpf.Design;

/// <summary>Designer data consumes the same library owner and original model defaults.</summary>
public sealed class NativeCrosshairDesignData : IDisposable
{
    private readonly NativeResultViewport _viewport = new();
    private readonly NativeRoiSamples.Image _image = new();
    private bool _disposed;
    public IReadOnlyList<CrosshairSettings> CrossDatas { get; }
    public NativeCrosshairDesignData()
    {
        try
        {
            var layer = _viewport.CreateCrosshairLayer();
            if (!_viewport.BindImage(_image, () => !_disposed)) throw new InvalidOperationException("辅助线设计图像未接纳。");
            layer.Activate(); CrossDatas = layer.CrossDatas;
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        try { _viewport.Dispose(); } finally { _image.Dispose(); }
    }
}
