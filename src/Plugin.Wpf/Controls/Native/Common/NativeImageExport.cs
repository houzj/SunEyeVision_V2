using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

namespace SunEyeVision.Plugin.Wpf.Native.Common;

/// <summary>Synchronous view export. Copies borrowed image buffers; never changes workflow inputs.</summary>
internal static partial class NativeImageExport
{
    public static BitmapSource PrepareRaw(IImageData image) => CreateBitmap(image);

    public static BitmapSource PrepareRendered(IImageData image, IReadOnlyList<object>? shapes, FrameworkElement owner)
    {
        return PrepareRenderedBitmap(CreateBitmap(image), shapes, owner);
    }

    internal static BitmapSource PrepareRenderedBitmap(BitmapSource bitmap, IReadOnlyList<object>? shapes, FrameworkElement owner)
    {
        owner.Dispatcher.VerifyAccess();
        if (bitmap.Format == PixelFormats.Gray16 || bitmap.Format == PixelFormats.Rgb48)
            throw new NotSupportedException("16位带图形导出未实现：原件没有已证颜色映射；原图仍可无损PNG导出。");
        using var source = ToMat(bitmap);
        using var rendered = new OpenCvSharp.Mat();
        if (source.Channels() == 1)
            OpenCvSharp.Cv2.CvtColor(source, rendered, OpenCvSharp.ColorConversionCodes.GRAY2BGR);
        else source.CopyTo(rendered);
        foreach (var item in shapes ?? Array.Empty<object>())
        {
            if (item is IShape shape) DrawNativeShape(rendered, shape);
            else if (item is Tuple<string, object> tuple && tuple.Item2 is List<object> group)
                foreach (var member in group)
                    if (member is IShape child) DrawNativeShape(rendered, child);
        }
        return FromMat(rendered);
    }
    private static BitmapSource CreateBitmap(IImageData image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0) throw new ArgumentException("图像尺寸无效。", nameof(image));
        var snapshot = new ImageSnapshot(image);
        var format = snapshot.DetailedPixelFormat == 17825976u ? PixelFormats.Gray16 : snapshot.PixelFormat switch
        {
            "Rgb24" or "Rgb24_P3" => PixelFormats.Rgb24,
            "Bgr24" => PixelFormats.Bgr24,
            "Bgra32" => PixelFormats.Bgra32,
            "Pbgra32" => PixelFormats.Pbgra32,
            "Gray16" => PixelFormats.Gray16,
            _ => PixelFormats.Gray8
        };
        var stride = checked(snapshot.Width * format.BitsPerPixel / 8);
        var expected = checked(stride * snapshot.Height);
        var pixels = format == PixelFormats.Gray16 ? snapshot.RangeBuffer ?? snapshot.Buffer : snapshot.Buffer;
        if (pixels is null)
        {
            if (string.IsNullOrEmpty(snapshot.MemoryAddress)) throw new InvalidOperationException("图像缓冲区缺失。");
            using var mapping = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(snapshot.MemoryAddress, System.IO.MemoryMappedFiles.MemoryMappedFileRights.Read);
            using var view = mapping.CreateViewAccessor(0,0,System.IO.MemoryMappedFiles.MemoryMappedFileAccess.Read);
            if (view.Capacity < 16) throw new InvalidDataException("共享图像头不完整。");
            var header = new byte[16]; view.ReadArray(0,header,0,header.Length);
            var length = BitConverter.ToInt32(header,0); var offset = BitConverter.ToInt16(header,14);
            if (header[8] != 0 || length != expected || offset < 16 || checked((long)offset + expected) > view.Capacity)
                throw new InvalidDataException("共享图像忙、像素长度或数据偏移无效。");
            pixels = new byte[expected];
            if (view.ReadArray(offset,pixels,0,expected) != expected) throw new InvalidDataException("共享图像像素不完整。");
            var after = new byte[16]; view.ReadArray(0,after,0,after.Length);
            if (after[8] != 0 || !after.SequenceEqual(header))
                throw new InvalidDataException("共享图像在采样期间改变，请重新导出。");
            // Header recheck is a bounded best-effort sample, not an ABA-proof transport protocol.
        }
        if (pixels.Length != expected) throw new InvalidDataException("图像像素长度不匹配。");
        if (snapshot.PixelFormat == "Rgb24_P3")
        {
            var plane = checked(snapshot.Width * snapshot.Height); var interleaved = new byte[expected];
            for (int index=0;index<plane;index++)
            {interleaved[index*3]=pixels[index];interleaved[index*3+1]=pixels[plane+index];interleaved[index*3+2]=pixels[plane*2+index];}
            pixels = interleaved;
        }
        var bitmap = BitmapSource.Create(snapshot.Width,snapshot.Height,96,96,format,null,pixels,stride);
        bitmap.Freeze(); return bitmap;
    }
    public static void SavePrepared(string path, BitmapSource bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (!bitmap.IsFrozen) throw new ArgumentException("导出必须使用弹窗前冻结的完整位图。", nameof(bitmap));
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".png" or ".bmp" or ".jpg" or ".jpeg"))
            throw new NotSupportedException("不支持的图像文件格式。");
        using var image = ToMat(bitmap);
        // The stock right-click entry has no explicit quality. Do not silently drop
        // the additional host formats' alpha or 16-bit samples in a narrower codec.
        if (image.Depth() != OpenCvSharp.MatType.CV_8U && extension != ".png")
            throw new NotSupportedException("16位图像仅支持无损PNG导出，其他编码不能保证保留原始位深。");
        if (image.Channels() == 4 && extension != ".png")
        {
            var values = new byte[checked(image.Width * image.Height * 4)];
            System.Runtime.InteropServices.Marshal.Copy(image.Data, values, 0, values.Length);
            if (Enumerable.Range(0, image.Width * image.Height).Any(index => values[index * 4 + 3] != 255))
                throw new NotSupportedException("含透明像素的图像仅支持PNG导出，其他编码不能保证保留透明度。");
            OpenCvSharp.Cv2.CvtColor(image, image, OpenCvSharp.ColorConversionCodes.BGRA2BGR);
        }
        if (!OpenCvSharp.Cv2.ImEncode(extension, image, out byte[] encoded))
            throw new IOException("图像编码失败。");
        var target = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(target)!;
        var temporary = Path.Combine(directory, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(encoded); stream.Flush(true); }
            File.Move(temporary, target, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
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
