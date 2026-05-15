using HalconDotNet;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.SDK.Models.Imaging
{
    public interface IImageSource : IDisposable
    {
        string Id { get; }
        string SourceType { get; }
        ImageMetadata? Metadata { get; }
        BitmapSource? GetThumbnail(int size = 60);
        BitmapSource? GetFullImage();
        HImage? GetHImage();

        Task<BitmapSource?> GetThumbnailAsync(int size = 60, CancellationToken cancellationToken = default);
        Task<BitmapSource?> GetFullImageAsync(CancellationToken cancellationToken = default);
        Task<HImage?> GetHImageAsync(CancellationToken cancellationToken = default);

        ImageLoadState GetLoadState(ImageFormat format);
        void Preload(ImageFormat formats, CancellationToken cancellationToken = default);
        Task PreloadAsync(ImageFormat formats, CancellationToken cancellationToken = default);
        void Release(ImageFormat formats);
        event EventHandler<ImageLoadedEventArgs>? ImageLoaded;
    }
}
