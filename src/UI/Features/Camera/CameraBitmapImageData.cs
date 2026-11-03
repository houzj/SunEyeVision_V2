using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.PresentationControls.RenderInterface;

namespace SunEyeVision.UI.Features.Camera;

/// <summary>相机预览位图的独立像素副本，供原件视口读取。</summary>
internal sealed class CameraBitmapImageData : IImageData
{
    private readonly byte[] _pixels;
    private CameraBitmapImageData(int width, int height, byte[] pixels) => (Width, Height, _pixels) = (width, height, pixels);
    public int Width { get; }
    public int Height { get; }
    public byte[] Buffer { get => _pixels; set => throw new InvalidOperationException("相机预览像素不可替换。"); }
    public string MemoryAddress => string.Empty;
    public string PixelFormat => "Bgra32";
    public static CameraBitmapImageData Create(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        BitmapSource converted = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = checked(converted.PixelWidth * 4);
        var pixels = new byte[checked(stride * converted.PixelHeight)];
        converted.CopyPixels(pixels, stride, 0);
        return new CameraBitmapImageData(converted.PixelWidth, converted.PixelHeight, pixels);
    }
}
