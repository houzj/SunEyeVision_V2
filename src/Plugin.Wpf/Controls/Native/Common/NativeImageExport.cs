using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.BaseData;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

namespace SunEyeVision.Plugin.Wpf.Native.Common;

/// <summary>Synchronous view export. Copies borrowed image buffers; never changes workflow inputs.</summary>
internal static class NativeImageExport
{
    public static void SaveRaw(string path, IImageData image) => Encode(path, CreateBitmap(image));

    public static void SaveRendered(string path, IImageData image, IReadOnlyList<object>? shapes, FrameworkElement owner)
    {
        owner.Dispatcher.VerifyAccess();
        var bitmap = CreateBitmap(image);
        var size = new Size(bitmap.PixelWidth, bitmap.PixelHeight);
        var root = new Grid { Width = size.Width, Height = size.Height, ClipToBounds = true };
        // Reuse the original shape page and all its exact template dependencies.
        root.Resources.MergedDictionaries.Add(owner.Resources);
        root.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/SunEyeVision.Plugin.Wpf;component/Themes/Native/Display/VMBaseControls/themes/generic.xaml", UriKind.Relative)
        });
        var page = new ShapeItemsPage
        {
            Width = size.Width, Height = size.Height,
            ImageSourceWidth = bitmap.PixelWidth, ImageSourceHeight = bitmap.PixelHeight,
            ControlToImageRadio = 1, ThicknessScale = 1,
            ItemsSource = new ObservableCollection<object>(shapes ?? Array.Empty<object>())
        };
        root.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Fill });
        root.Children.Add(page);
        try
        {
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();
            var rendered = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32);
            rendered.Render(root);
            rendered.Freeze();
            Encode(path, rendered);
        }
        finally
        {
            page.ItemsSource = null;
            root.Children.Clear();
            root.Resources.MergedDictionaries.Clear();
        }
    }

    private static BitmapSource CreateBitmap(IImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0) throw new ArgumentException("图像尺寸无效。", nameof(image));
        var snapshot = new ImageSnapshot(image);
        var converter = new Byte2ImageSource();
        try
        {
            // Preserve the original 16-bit range buffer for raw PNG/BMP export.
            if (snapshot.DetailedPixelFormat == 17825976u)
            {
                var pixels = snapshot.RangeBuffer ?? snapshot.Buffer ?? converter.GetImageBufferByFileName(
                    snapshot.MemoryAddress, snapshot.Width, snapshot.Height, PixelFormats.Gray16,
                    snapshot.DetailedPixelFormat, isDirect: false);
                var stride = checked(snapshot.Width * 2);
                if (pixels is null || pixels.Length < checked(stride * snapshot.Height))
                    throw new InvalidOperationException("16位图像缓冲区不完整。");
                var gray = BitmapSource.Create(snapshot.Width, snapshot.Height, 96, 96, PixelFormats.Gray16, null, pixels, stride);
                gray.Freeze();
                return gray;
            }
            var source = converter.BuiltImageSource(snapshot) as BitmapSource
                ?? throw new InvalidOperationException("无法从图像缓冲区生成位图。");
            var copy = source.Clone();
            copy.Freeze();
            return copy;
        }
        finally { converter.Clear(); }
    }

    private static void Encode(string path, BitmapSource bitmap)
    {
        BitmapEncoder encoder = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => new PngBitmapEncoder(),
            ".bmp" => new BmpBitmapEncoder(),
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
            _ => throw new NotSupportedException("不支持的图像文件格式。")
        };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    // Ephemeral export copy, not a persisted image or a second workflow model.
    private sealed class ImageSnapshot : IImageData, IImageDataExtern, IRangeImageData
    {
        public ImageSnapshot(IImageData source)
        {
            Width = source.Width; Height = source.Height; Buffer = source.Buffer?.ToArray()!;
            MemoryAddress = source.MemoryAddress; PixelFormat = source.PixelFormat;
            if (source is IImageDataExtern extra)
            {
                NeedConvertColor = extra.NeedConvertColor; PixelInfoLabel = extra.PixelInfoLabel;
                PixelExtraInfo = new Dictionary<string, string>(extra.PixelExtraInfo);
                NeedCheckExecuteCount = extra.NeedCheckExecuteCount; ExecuteCount = extra.ExecuteCount;
            }
            if (source is IRangeImageData range)
            {
                RangeBuffer = range.RangeBuffer?.ToArray()!; DetailedPixelFormat = range.DetailedPixelFormat;
                Xoffset = range.Xoffset; Yoffset = range.Yoffset; Zoffset = range.Zoffset;
                Xscale = range.Xscale; Yscale = range.Yscale; Zscale = range.Zscale;
            }
        }
        public int Width { get; }
        public int Height { get; }
        public byte[] Buffer { get; set; }
        public string MemoryAddress { get; }
        public string PixelFormat { get; private set; }
        public bool NeedConvertColor { get; }
        public string PixelInfoLabel { get; } = "";
        public Dictionary<string, string> PixelExtraInfo { get; } = new();
        public bool NeedCheckExecuteCount { get; }
        public uint ExecuteCount { get; }
        public void SetPixelFormat(string format) => PixelFormat = format;
        public byte[] RangeBuffer { get; set; } = null!;
        public uint DetailedPixelFormat { get; set; }
        public int Xoffset { get; set; }
        public int Yoffset { get; set; }
        public int Zoffset { get; set; }
        public int Xscale { get; set; }
        public int Yscale { get; set; }
        public int Zscale { get; set; }
    }
}
