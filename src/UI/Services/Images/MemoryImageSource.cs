using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Imaging;
using SunEyeVision.Plugin.SDK.Models.Imaging;

namespace SunEyeVision.UI.Services.Images
{
    /// <summary>
    /// 内存图像源 - 从内存Mat创建的IImageSource实现
    /// </summary>
    /// <remarks>
    /// 用于算法处理结果图像，直接持有Mat对象，按需转换为BitmapSource。
    /// </remarks>
    public sealed class MemoryImageSource : IImageSource
    {
        private readonly object _lock = new();

        // 原始Mat数据
        private Mat? _originalMat;

        // IVisionImage缓存（当从IVisionImage创建或首次从Mat转换后持有）
        private IVisionImage? _visionImage;

        // 转换后的BitmapSource
        private BitmapSource? _thumbnail;
        private BitmapSource? _fullImage;

        // 加载状态
        private ImageLoadState _thumbnailState = ImageLoadState.NotLoaded;
        private ImageLoadState _fullImageState = ImageLoadState.NotLoaded;
        private ImageLoadState _matState;

        // 是否拥有Mat的所有权
        private readonly bool _ownsMat;

        // 元数据
        private ImageMetadata? _metadata;

        // 是否已释放
        private bool _disposed;

        // 缩略图尺寸
        private int _thumbnailSize = 60;

        /// <summary>
        /// 图像源唯一标识符
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 图像源类型
        /// </summary>
        public string SourceType => "Memory";

        /// <summary>
        /// 图像元数据
        /// </summary>
        public ImageMetadata? Metadata
        {
            get
            {
                if (_metadata == null && _originalMat != null)
                {
                    _metadata = CreateMetadataFromMat(_originalMat);
                }
                return _metadata;
            }
        }

        /// <summary>
        /// 从Mat创建内存图像源
        /// </summary>
        /// <param name="mat">Mat对象</param>
        /// <param name="id">可选ID</param>
        /// <param name="ownsMat">是否拥有Mat所有权，true则会在此对象释放时释放Mat</param>
        public MemoryImageSource(Mat mat, string? id = null, bool ownsMat = true)
        {
            _originalMat = mat ?? throw new ArgumentNullException(nameof(mat));
            _ownsMat = ownsMat;
            Id = id ?? $"memory://{Guid.NewGuid():N}";
            _matState = ImageLoadState.Loaded;
        }

        /// <summary>
        /// 从BitmapSource创建内存图像源
        /// </summary>
        /// <param name="bitmapSource">BitmapSource对象</param>
        /// <param name="id">可选ID</param>
        public MemoryImageSource(BitmapSource bitmapSource, string? id = null)
        {
            if (bitmapSource == null)
                throw new ArgumentNullException(nameof(bitmapSource));

            _fullImage = bitmapSource;
            Id = id ?? $"memory://{Guid.NewGuid():N}";
            _ownsMat = false;
                _fullImageState = ImageLoadState.Loaded;
            _matState = ImageLoadState.NotLoaded;

            _metadata = new ImageMetadata
            {
                Width = bitmapSource.PixelWidth,
                Height = bitmapSource.PixelHeight,
                PixelFormat = ConvertPixelFormat(bitmapSource.Format),
                BitsPerPixel = bitmapSource.Format.BitsPerPixel,
                Channels = bitmapSource.Format.BitsPerPixel / 8
            };
        }

        /// <summary>
        /// 从IVisionImage创建内存图像源
        /// </summary>
        /// <param name="image">IVisionImage图像</param>
        /// <param name="id">可选ID</param>
        public MemoryImageSource(IVisionImage image, string? id = null)
        {
            _visionImage = image ?? throw new ArgumentNullException(nameof(image));
            _ownsMat = false;
            Id = id ?? $"memory://{Guid.NewGuid():N}";
            _matState = ImageLoadState.Loaded;

            _metadata = new ImageMetadata
            {
                Width = image.Width,
                Height = image.Height,
                PixelFormat = PixelFormat.Undefined,
                BitsPerPixel = image.Channels * 8,
                Channels = image.Channels
            };
        }

        /// <summary>
        /// 从Mat创建元数据
        /// </summary>
        private static ImageMetadata CreateMetadataFromMat(Mat mat)
        {
            return new ImageMetadata
            {
                Width = mat.Width,
                Height = mat.Height,
                PixelFormat = ConvertPixelFormat(mat.Type()),
                BitsPerPixel = mat.Type().Depth * mat.Channels(),
                Channels = mat.Channels()
            };
        }

        /// <summary>
        /// 转换像素格式
        /// </summary>
        private static PixelFormat ConvertPixelFormat(OpenCvSharp.MatType matType)
        {
            int channels = matType.Channels;
            int depth = matType.Depth;

            return channels switch
            {
                1 => depth switch
                {
                    MatType.CV_8U => PixelFormat.Mono8,
                    MatType.CV_16U => PixelFormat.Mono16,
                    _ => PixelFormat.Undefined
                },
                3 => depth switch
                {
                    MatType.CV_8U => PixelFormat.RGB24,
                    _ => PixelFormat.Undefined
                },
                4 => depth switch
                {
                    MatType.CV_8U => PixelFormat.RGBA32,
                    _ => PixelFormat.Undefined
                },
                _ => PixelFormat.Undefined
            };
        }

        /// <summary>
        /// 转换WPF像素格式
        /// </summary>
        private static PixelFormat ConvertPixelFormat(System.Windows.Media.PixelFormat wpfFormat)
        {
            if (wpfFormat == System.Windows.Media.PixelFormats.Gray8)
                return PixelFormat.Mono8;
            if (wpfFormat == System.Windows.Media.PixelFormats.Gray16)
                return PixelFormat.Mono16;
            if (wpfFormat == System.Windows.Media.PixelFormats.Rgb24)
                return PixelFormat.RGB24;
            if (wpfFormat == System.Windows.Media.PixelFormats.Bgr24)
                return PixelFormat.RGB24;
            if (wpfFormat == System.Windows.Media.PixelFormats.Bgr32 || wpfFormat == System.Windows.Media.PixelFormats.Bgra32)
                return PixelFormat.RGBA32;

            return PixelFormat.Undefined;
        }

        /// <summary>
        /// 获取缩略图
        /// </summary>
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

                // 优先从FullImage生成
                if (_fullImage != null)
                {
                    thumbnail = CreateThumbnailFromBitmapSource(_fullImage, size);
                }
                else if (_originalMat != null && !_originalMat.Empty())
                {
                    using var resized = ResizeMat(_originalMat, size);
                    thumbnail = resized != null ? MatToBitmapSource(resized) : null;
                }

                if (thumbnail != null)
                {
                    thumbnail.Freeze();
                    lock (_lock)
                    {
                        _thumbnail = thumbnail;
                        _thumbnailState = ImageLoadState.Loaded;
                    }

                    sw.Stop();
                    OnImageLoaded(ImageFormat.Thumbnail, true, null, sw.ElapsedMilliseconds);
                }

                return thumbnail;
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _thumbnailState = ImageLoadState.Failed;
                }
                OnImageLoaded(ImageFormat.Thumbnail, false, ex.Message, 0);
                return null;
            }
        }

        /// <summary>
        /// 获取完整显示图像
        /// </summary>
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

                if (_originalMat != null && !_originalMat.Empty())
                {
                    fullImage = MatToBitmapSource(_originalMat);
                    fullImage.Freeze();
                }

                if (fullImage != null)
                {
                    lock (_lock)
                    {
                        _fullImage = fullImage;
                        _fullImageState = ImageLoadState.Loaded;
                    }

                    sw.Stop();
                    OnImageLoaded(ImageFormat.FullImage, true, null, sw.ElapsedMilliseconds);
                }

                return fullImage;
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _fullImageState = ImageLoadState.Failed;
                }
                OnImageLoaded(ImageFormat.FullImage, false, ex.Message, 0);
                return null;
            }
        }

        /// <summary>
        /// 获取Mat格式图像
        /// </summary>
        public IVisionImage? GetMat()
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                if (_visionImage != null)
                    return _visionImage;

                if (_originalMat == null)
                    return null;

                // 从 Mat 提取像素数据通过 Factory 创建 IVisionImage
                var pixelData = new byte[_originalMat.Total() * _originalMat.ElemSize()];
                System.Runtime.InteropServices.Marshal.Copy(_originalMat.Data, pixelData, 0, pixelData.Length);
                _visionImage = VisionAdapter.Factory.LoadFromBytes(pixelData, _originalMat.Width, _originalMat.Height, _originalMat.Channels());
                return _visionImage;
            }
        }

        /// <summary>
        /// 异步获取缩略图
        /// </summary>
        public Task<BitmapSource?> GetThumbnailAsync(int size = 60, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => GetThumbnail(size), cancellationToken);
        }

        /// <summary>
        /// 异步获取完整显示图像
        /// </summary>
        public Task<BitmapSource?> GetFullImageAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(GetFullImage, cancellationToken);
        }

        /// <summary>
        /// 异步获取Mat格式图像
        /// </summary>
        public Task<IVisionImage?> GetMatAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(GetMat, cancellationToken);
        }

        /// <summary>
        /// 获取加载状态
        /// </summary>
        public ImageLoadState GetLoadState(ImageFormat format)
        {
            return format switch
            {
                ImageFormat.Thumbnail => _thumbnailState,
                ImageFormat.FullImage => _fullImageState,
                ImageFormat.Mat => _matState,
                _ => ImageLoadState.NotLoaded
            };
        }

        /// <summary>
        /// 预加载
        /// </summary>
        public void Preload(ImageFormat formats, CancellationToken cancellationToken = default)
        {
            if (formats.HasFlag(ImageFormat.Thumbnail))
                Task.Run(() => GetThumbnail(_thumbnailSize), cancellationToken);

            if (formats.HasFlag(ImageFormat.FullImage))
                Task.Run(GetFullImage, cancellationToken);
        }

        /// <summary>
        /// 异步预加载
        /// </summary>
        public async Task PreloadAsync(ImageFormat formats, CancellationToken cancellationToken = default)
        {
            var tasks = new System.Collections.Generic.List<Task>();

            if (formats.HasFlag(ImageFormat.Thumbnail))
                tasks.Add(GetThumbnailAsync(_thumbnailSize, cancellationToken));

            if (formats.HasFlag(ImageFormat.FullImage))
                tasks.Add(GetFullImageAsync(cancellationToken));

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 释放指定格式的数据
        /// </summary>
        public void Release(ImageFormat formats)
        {
            lock (_lock)
            {
                if (formats.HasFlag(ImageFormat.Thumbnail))
                {
                    _thumbnail = null;
                    _thumbnailState = ImageLoadState.Released;
                }

                if (formats.HasFlag(ImageFormat.FullImage))
                {
                    _fullImage = null;
                    _fullImageState = ImageLoadState.Released;
                }

                // Mat不释放，因为MemoryImageSource可能需要继续使用
                if (formats.HasFlag(ImageFormat.Mat) && !_ownsMat)
                {
                    _matState = ImageLoadState.Released;
                }
            }
        }

        /// <summary>
        /// 图像加载完成事件
        /// </summary>
        public event EventHandler<ImageLoadedEventArgs>? ImageLoaded;

        /// <summary>
        /// 触发图像加载完成事件
        /// </summary>
        private void OnImageLoaded(ImageFormat format, bool success, string? errorMessage, long loadTimeMs)
        {
            var args = success
                ? new ImageLoadedEventArgs(Id, format, loadTimeMs)
                : new ImageLoadedEventArgs(Id, format, errorMessage ?? "未知错误", loadTimeMs);

            ImageLoaded?.Invoke(this, args);
        }

        /// <summary>
        /// 从BitmapSource创建缩略图
        /// </summary>
        private static BitmapSource CreateThumbnailFromBitmapSource(BitmapSource source, int size)
        {
            double scale = (double)size / Math.Max(source.PixelWidth, source.PixelHeight);
            int newWidth = (int)(source.PixelWidth * scale);
            int newHeight = (int)(source.PixelHeight * scale);

            var transformedBitmap = new TransformedBitmap(source, new System.Windows.Media.ScaleTransform(
                (double)newWidth / source.PixelWidth,
                (double)newHeight / source.PixelHeight));

            transformedBitmap.Freeze();
            return transformedBitmap;
        }

        /// <summary>
        /// 缩放Mat
        /// </summary>
        private static Mat? ResizeMat(Mat mat, int size)
        {
            if (mat == null || mat.Empty()) return null;

            int maxSize = Math.Max(mat.Width, mat.Height);
            double scale = (double)size / maxSize;
            int newWidth = (int)(mat.Width * scale);
            int newHeight = (int)(mat.Height * scale);

            return mat.Resize(new Size(newWidth, newHeight), 0, 0, InterpolationFlags.Linear);
        }

        /// <summary>
        /// Mat → BitmapSource 手动转换（替代 OpenCvSharp.WpfExtensions）
        /// </summary>
        private static BitmapSource MatToBitmapSource(Mat mat)
        {
            var pixelFormat = mat.Channels() switch
            {
                1 => System.Windows.Media.PixelFormats.Gray8,
                3 => System.Windows.Media.PixelFormats.Bgr24,
                4 => System.Windows.Media.PixelFormats.Bgra32,
                _ => System.Windows.Media.PixelFormats.Gray8
            };
            int stride = mat.Width * mat.Channels();
            var bytes = new byte[stride * mat.Height];
            System.Runtime.InteropServices.Marshal.Copy(mat.Data, bytes, 0, bytes.Length);
            var bitmap = BitmapSource.Create(
                mat.Width, mat.Height, 96, 96, pixelFormat, null, bytes, stride);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// 抛出已释放异常
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MemoryImageSource));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;

                _thumbnail = null;
                _fullImage = null;

                if (_ownsMat && _originalMat != null)
                {
                    _originalMat.Dispose();
                    _originalMat = null;
                }

                _visionImage?.Dispose();
                _visionImage = null;

                _disposed = true;
            }
        }
    }
}
