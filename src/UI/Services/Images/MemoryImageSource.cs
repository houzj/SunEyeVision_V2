using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models.Imaging;
using SunEyeVision.UI.Extensions;

namespace SunEyeVision.UI.Services.Images
{
    public sealed class MemoryImageSource : IImageSource
    {
        private readonly object _lock = new();

        private HImage? _originalImage;
        private BitmapSource? _thumbnail;
        private BitmapSource? _fullImage;

        private ImageLoadState _thumbnailState = ImageLoadState.NotLoaded;
        private ImageLoadState _fullImageState = ImageLoadState.NotLoaded;
        private ImageLoadState _hImageState;

        private readonly bool _ownsImage;
        private ImageMetadata? _metadata;
        private bool _disposed;
        private int _thumbnailSize = 60;

        public string Id { get; }
        public string SourceType => "Memory";

        public ImageMetadata? Metadata
        {
            get
            {
                if (_metadata == null && _originalImage != null)
                    _metadata = CreateMetadataFromHImage(_originalImage);
                return _metadata;
            }
        }

        public MemoryImageSource(HImage image, string? id = null, bool ownsImage = true)
        {
            _originalImage = image ?? throw new ArgumentNullException(nameof(image));
            _ownsImage = ownsImage;
            Id = id ?? $"memory://{Guid.NewGuid():N}";
            _hImageState = ImageLoadState.Loaded;
        }

        public MemoryImageSource(BitmapSource bitmapSource, string? id = null)
        {
            if (bitmapSource == null) throw new ArgumentNullException(nameof(bitmapSource));
            _fullImage = bitmapSource;
            Id = id ?? $"memory://{Guid.NewGuid():N}";
            _ownsImage = false;
            _fullImageState = ImageLoadState.Loaded;
            _hImageState = ImageLoadState.NotLoaded;

            _metadata = new ImageMetadata
            {
                Width = bitmapSource.PixelWidth,
                Height = bitmapSource.PixelHeight,
                PixelFormat = ConvertWpfPixelFormat(bitmapSource.Format),
                BitsPerPixel = bitmapSource.Format.BitsPerPixel,
                Channels = bitmapSource.Format.BitsPerPixel / 8
            };
        }

        private static ImageMetadata CreateMetadataFromHImage(HImage image)
        {
            var info = image.GetInfo();
            return new ImageMetadata
            {
                Width = info.Width,
                Height = info.Height,
                PixelFormat = ConvertPixelFormat(info.Channels, info.Type),
                BitsPerPixel = info.Channels * 8,
                Channels = info.Channels
            };
        }

        private static PixelFormat ConvertPixelFormat(int channels, string type)
        {
            if (type == "byte")
            {
                return channels switch
                {
                    1 => PixelFormat.Mono8,
                    3 => PixelFormat.RGB24,
                    4 => PixelFormat.RGBA32,
                    _ => PixelFormat.Undefined
                };
            }
            if (type == "uint2" && channels == 1) return PixelFormat.Mono16;
            return PixelFormat.Undefined;
        }

        private static PixelFormat ConvertWpfPixelFormat(System.Windows.Media.PixelFormat wpfFormat)
        {
            if (wpfFormat == System.Windows.Media.PixelFormats.Gray8) return PixelFormat.Mono8;
            if (wpfFormat == System.Windows.Media.PixelFormats.Gray16) return PixelFormat.Mono16;
            if (wpfFormat == System.Windows.Media.PixelFormats.Rgb24) return PixelFormat.RGB24;
            if (wpfFormat == System.Windows.Media.PixelFormats.Bgr24) return PixelFormat.RGB24;
            if (wpfFormat == System.Windows.Media.PixelFormats.Bgr32 || wpfFormat == System.Windows.Media.PixelFormats.Bgra32)
                return PixelFormat.RGBA32;
            return PixelFormat.Undefined;
        }

        public BitmapSource? GetThumbnail(int size = 60)
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                if (_thumbnailState == ImageLoadState.Loaded && _thumbnail != null)
                    return _thumbnail;
                _thumbnailSize = size;
            }

            try
            {
                var sw = Stopwatch.StartNew();
                BitmapSource? thumbnail = null;

                if (_fullImage != null)
                {
                    thumbnail = CreateThumbnailFromBitmapSource(_fullImage, size);
                }
                else if (_originalImage != null && _originalImage.IsInitialized())
                {
                    using var resized = _originalImage.ZoomToFit(size);
                    thumbnail = resized?.ToBitmapSource();
                }

                if (thumbnail != null)
                {
                    thumbnail.Freeze();
                    lock (_lock) { _thumbnail = thumbnail; _thumbnailState = ImageLoadState.Loaded; }
                    sw.Stop();
                    OnImageLoaded(ImageFormat.Thumbnail, true, null, sw.ElapsedMilliseconds);
                }

                return thumbnail;
            }
            catch (Exception ex)
            {
                lock (_lock) { _thumbnailState = ImageLoadState.Failed; }
                OnImageLoaded(ImageFormat.Thumbnail, false, ex.Message, 0);
                return null;
            }
        }

        public BitmapSource? GetFullImage()
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                if (_fullImageState == ImageLoadState.Loaded && _fullImage != null)
                    return _fullImage;
            }

            try
            {
                var sw = Stopwatch.StartNew();
                BitmapSource? fullImage = null;

                if (_originalImage != null && _originalImage.IsInitialized())
                {
                    fullImage = _originalImage.ToBitmapSource();
                    if (fullImage != null) fullImage.Freeze();
                }

                if (fullImage != null)
                {
                    lock (_lock) { _fullImage = fullImage; _fullImageState = ImageLoadState.Loaded; }
                    sw.Stop();
                    OnImageLoaded(ImageFormat.FullImage, true, null, sw.ElapsedMilliseconds);
                }

                return fullImage;
            }
            catch (Exception ex)
            {
                lock (_lock) { _fullImageState = ImageLoadState.Failed; }
                OnImageLoaded(ImageFormat.FullImage, false, ex.Message, 0);
                return null;
            }
        }

        public HImage? GetHImage()
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                return _originalImage;
            }
        }

        public Task<BitmapSource?> GetThumbnailAsync(int size = 60, CancellationToken cancellationToken = default)
            => Task.Run(() => GetThumbnail(size), cancellationToken);

        public Task<BitmapSource?> GetFullImageAsync(CancellationToken cancellationToken = default)
            => Task.Run(GetFullImage, cancellationToken);

        public Task<HImage?> GetHImageAsync(CancellationToken cancellationToken = default)
            => Task.Run(GetHImage, cancellationToken);

        public ImageLoadState GetLoadState(ImageFormat format) => format switch
        {
            ImageFormat.Thumbnail => _thumbnailState,
            ImageFormat.FullImage => _fullImageState,
            ImageFormat.Mat => _hImageState,
            _ => ImageLoadState.NotLoaded
        };

        public void Preload(ImageFormat formats, CancellationToken cancellationToken = default)
        {
            if (formats.HasFlag(ImageFormat.Thumbnail))
                Task.Run(() => GetThumbnail(_thumbnailSize), cancellationToken);
            if (formats.HasFlag(ImageFormat.FullImage))
                Task.Run(GetFullImage, cancellationToken);
        }

        public async Task PreloadAsync(ImageFormat formats, CancellationToken cancellationToken = default)
        {
            var tasks = new System.Collections.Generic.List<Task>();
            if (formats.HasFlag(ImageFormat.Thumbnail))
                tasks.Add(GetThumbnailAsync(_thumbnailSize, cancellationToken));
            if (formats.HasFlag(ImageFormat.FullImage))
                tasks.Add(GetFullImageAsync(cancellationToken));
            await Task.WhenAll(tasks);
        }

        public void Release(ImageFormat formats)
        {
            lock (_lock)
            {
                if (formats.HasFlag(ImageFormat.Thumbnail)) { _thumbnail = null; _thumbnailState = ImageLoadState.Released; }
                if (formats.HasFlag(ImageFormat.FullImage)) { _fullImage = null; _fullImageState = ImageLoadState.Released; }
                if (formats.HasFlag(ImageFormat.Mat) && !_ownsImage)
                    _hImageState = ImageLoadState.Released;
            }
        }

        public event EventHandler<ImageLoadedEventArgs>? ImageLoaded;

        private void OnImageLoaded(ImageFormat format, bool success, string? errorMessage, long loadTimeMs)
        {
            var args = success
                ? new ImageLoadedEventArgs(Id, format, loadTimeMs)
                : new ImageLoadedEventArgs(Id, format, errorMessage ?? "未知错误", loadTimeMs);
            ImageLoaded?.Invoke(this, args);
        }

        private static BitmapSource CreateThumbnailFromBitmapSource(BitmapSource source, int size)
        {
            double scale = (double)size / Math.Max(source.PixelWidth, source.PixelHeight);
            int newWidth = (int)(source.PixelWidth * scale);
            int newHeight = (int)(source.PixelHeight * scale);
            var transformedBitmap = new TransformedBitmap(source,
                new System.Windows.Media.ScaleTransform(
                    (double)newWidth / source.PixelWidth,
                    (double)newHeight / source.PixelHeight));
            transformedBitmap.Freeze();
            return transformedBitmap;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MemoryImageSource));
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _thumbnail = null;
                _fullImage = null;
                if (_ownsImage && _originalImage != null)
                {
                    _originalImage.Dispose();
                    _originalImage = null;
                }
                _disposed = true;
            }
        }
    }
}
