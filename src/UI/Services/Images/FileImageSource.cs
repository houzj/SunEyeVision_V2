using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models.Imaging;
using SunEyeVision.UI.Extensions;

namespace SunEyeVision.UI.Services.Images
{
    public sealed class FileImageSource : IImageSource
    {
        private readonly string _filePath;
        private readonly object _lock = new();

        private BitmapSource? _thumbnail;
        private BitmapSource? _fullImage;
        private HImage? _hImage;

        private ImageLoadState _thumbnailState = ImageLoadState.NotLoaded;
        private ImageLoadState _fullImageState = ImageLoadState.NotLoaded;
        private ImageLoadState _hImageState = ImageLoadState.NotLoaded;

        private ImageMetadata? _metadata;
        private bool _metadataLoaded;
        private bool _disposed;
        private int _thumbnailSize = 60;

        public string Id { get; }
        public string SourceType => "File";

        public ImageMetadata? Metadata
        {
            get
            {
                if (!_metadataLoaded) LoadMetadata();
                return _metadata;
            }
        }

        public string FilePath => _filePath;

        public FileImageSource(string filePath, string? id = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException("图像文件不存在", filePath);

            _filePath = System.IO.Path.GetFullPath(filePath);
            Id = id ?? $"file://{_filePath}";
        }

        private void LoadMetadata()
        {
            lock (_lock)
            {
                if (_metadataLoaded) return;
                try
                {
                    var fileInfo = new FileInfo(_filePath);
                    using var image = HImageExtensions.ReadImageFile(_filePath);
                    var info = image?.GetInfo() ?? (0, 0, 1, "byte");

                    _metadata = new ImageMetadata
                    {
                        Width = info.Width,
                        Height = info.Height,
                        PixelFormat = ConvertPixelFormat(info.Channels, info.Type),
                        FileSize = fileInfo.Length,
                        FilePath = _filePath,
                        FileName = fileInfo.Name,
                        Extension = fileInfo.Extension,
                        CreatedTime = fileInfo.CreationTime,
                        ModifiedTime = fileInfo.LastWriteTime,
                        BitsPerPixel = info.Channels * 8,
                        Channels = info.Channels
                    };
                    _metadataLoaded = true;
                }
                catch
                {
                    _metadata = ImageMetadata.Empty;
                    _metadataLoaded = true;
                }
            }
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
            if (type == "uint2") return channels == 1 ? PixelFormat.Mono16 : PixelFormat.Undefined;
            return PixelFormat.Undefined;
        }

        public BitmapSource? GetThumbnail(int size = 60)
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                if (_thumbnailState == ImageLoadState.Loaded && _thumbnail != null)
                    return _thumbnail;
                if (_thumbnailState == ImageLoadState.Loading)
                    return null;
                _thumbnailState = ImageLoadState.Loading;
                _thumbnailSize = size;
            }

            try
            {
                var sw = Stopwatch.StartNew();
                using var image = HImageExtensions.ReadImageFile(_filePath);
                if (image == null || !image.IsInitialized())
                {
                    OnImageLoaded(ImageFormat.Thumbnail, false, "无法读取图像文件", 0);
                    return null;
                }

                using var resized = image.ZoomToFit(size);
                if (resized == null)
                {
                    OnImageLoaded(ImageFormat.Thumbnail, false, "缩放失败", 0);
                    return null;
                }

                var thumbnail = resized.ToBitmapSource();
                if (thumbnail != null)
                {
                    thumbnail.Freeze();
                    lock (_lock) { _thumbnail = thumbnail; _thumbnailState = ImageLoadState.Loaded; }
                }

                sw.Stop();
                OnImageLoaded(ImageFormat.Thumbnail, thumbnail != null, null, sw.ElapsedMilliseconds);
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
                if (_fullImageState == ImageLoadState.Loading)
                    return null;
                _fullImageState = ImageLoadState.Loading;
            }

            try
            {
                var sw = Stopwatch.StartNew();
                using var image = HImageExtensions.ReadImageFile(_filePath);
                if (image == null || !image.IsInitialized())
                {
                    OnImageLoaded(ImageFormat.FullImage, false, "无法读取图像文件", 0);
                    return null;
                }

                var fullImage = image.ToBitmapSource();
                if (fullImage != null) fullImage.Freeze();

                lock (_lock) { _fullImage = fullImage; _fullImageState = ImageLoadState.Loaded; }

                sw.Stop();
                OnImageLoaded(ImageFormat.FullImage, fullImage != null, null, sw.ElapsedMilliseconds);
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
                if (_hImageState == ImageLoadState.Loaded && _hImage != null)
                    return _hImage;
                if (_hImageState == ImageLoadState.Loading)
                    return null;
                _hImageState = ImageLoadState.Loading;
            }

            try
            {
                var sw = Stopwatch.StartNew();
                using var image = HImageExtensions.ReadImageFile(_filePath);
                if (image == null || !image.IsInitialized())
                {
                    OnImageLoaded(ImageFormat.Mat, false, "无法读取图像文件", 0);
                    return null;
                }

                lock (_lock) { _hImage = image; _hImageState = ImageLoadState.Loaded; }

                sw.Stop();
                OnImageLoaded(ImageFormat.Mat, true, null, sw.ElapsedMilliseconds);
                return image;
            }
            catch (Exception ex)
            {
                lock (_lock) { _hImageState = ImageLoadState.Failed; }
                OnImageLoaded(ImageFormat.Mat, false, ex.Message, 0);
                return null;
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
            if (formats.HasFlag(ImageFormat.Mat))
                Task.Run(GetHImage, cancellationToken);
        }

        public async Task PreloadAsync(ImageFormat formats, CancellationToken cancellationToken = default)
        {
            var tasks = new System.Collections.Generic.List<Task>();
            if (formats.HasFlag(ImageFormat.Thumbnail))
                tasks.Add(GetThumbnailAsync(_thumbnailSize, cancellationToken));
            if (formats.HasFlag(ImageFormat.FullImage))
                tasks.Add(GetFullImageAsync(cancellationToken));
            if (formats.HasFlag(ImageFormat.Mat))
                tasks.Add(GetHImageAsync(cancellationToken));
            await Task.WhenAll(tasks);
        }

        public void Release(ImageFormat formats)
        {
            lock (_lock)
            {
                if (formats.HasFlag(ImageFormat.Thumbnail)) { _thumbnail = null; _thumbnailState = ImageLoadState.Released; }
                if (formats.HasFlag(ImageFormat.FullImage)) { _fullImage = null; _fullImageState = ImageLoadState.Released; }
                if (formats.HasFlag(ImageFormat.Mat)) { _hImage?.Dispose(); _hImage = null; _hImageState = ImageLoadState.Released; }
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

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FileImageSource));
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _thumbnail = null;
                _fullImage = null;
                _hImage?.Dispose();
                _hImage = null;
                _disposed = true;
            }
        }
    }
}
