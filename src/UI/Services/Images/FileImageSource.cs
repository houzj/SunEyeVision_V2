using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Imaging;
using SunEyeVision.Plugin.SDK.Models.Imaging;

namespace SunEyeVision.UI.Services.Images
{
    /// <summary>
    /// 文件图像源 - 从文件加载图像的IImageSource实现
    /// </summary>
    /// <remarks>
    /// 支持延迟加载多种图像格式，线程安全，支持预加载和资源释放。
    /// </remarks>
    public sealed class FileImageSource : IImageSource
    {
        private readonly string _filePath;
        private readonly object _lock = new();

        // 缓存的图像数据
        private BitmapSource? _thumbnail;
        private BitmapSource? _fullImage;
        private Mat? _mat;
        private IVisionImage? _visionImage;

        // 加载状态
        private ImageLoadState _thumbnailState = ImageLoadState.NotLoaded;
        private ImageLoadState _fullImageState = ImageLoadState.NotLoaded;
        private ImageLoadState _matState = ImageLoadState.NotLoaded;

        // 元数据
        private ImageMetadata? _metadata;
        private bool _metadataLoaded;

        // 是否已释放
        private bool _disposed;

        // 缩略图默认尺寸
        private int _thumbnailSize = 60;

        /// <summary>
        /// 图像源唯一标识符
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 图像源类型
        /// </summary>
        public string SourceType => "File";

        /// <summary>
        /// 图像元数据
        /// </summary>
        public ImageMetadata? Metadata
        {
            get
            {
                if (!_metadataLoaded)
                {
                    LoadMetadata();
                }
                return _metadata;
            }
        }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath => _filePath;

        /// <summary>
        /// 创建文件图像源
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="id">可选ID，不提供则使用文件路径</param>
        public FileImageSource(string filePath, string? id = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("图像文件不存在", filePath);

            _filePath = System.IO.Path.GetFullPath(filePath);
            Id = id ?? $"file://{_filePath}";
        }

        /// <summary>
        /// 加载元数据
        /// </summary>
        private void LoadMetadata()
        {
            lock (_lock)
            {
                if (_metadataLoaded) return;

                try
                {
                    var fileInfo = new FileInfo(_filePath);
                    using var mat = Cv2.ImRead(_filePath, ImreadModes.Unchanged);

                    _metadata = new ImageMetadata
                    {
                        Width = mat?.Width ?? 0,
                        Height = mat?.Height ?? 0,
                        PixelFormat = ConvertPixelFormat(mat?.Type() ?? MatType.CV_8UC1),
                        FileSize = fileInfo.Length,
                        FilePath = _filePath,
                        FileName = fileInfo.Name,
                        Extension = fileInfo.Extension,
                        CreatedTime = fileInfo.CreationTime,
                        ModifiedTime = fileInfo.LastWriteTime,
                        BitsPerPixel = mat?.Channels() * 8 ?? 8,
                        Channels = mat?.Channels() ?? 1
                    };

                    _metadataLoaded = true;
                }
                catch (Exception)
                {
                    _metadata = ImageMetadata.Empty;
                    _metadataLoaded = true;
                }
            }
        }

        /// <summary>
        /// 转换像素格式
        /// </summary>
        private static PixelFormat ConvertPixelFormat(MatType matType)
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
        /// 获取缩略图
        /// </summary>
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
                using var mat = Cv2.ImRead(_filePath, ImreadModes.Color);
                if (mat == null || mat.Empty())
                {
                    OnImageLoaded(ImageFormat.Thumbnail, false, "无法读取图像文件", 0);
                    return null;
                }

                // 计算缩略图尺寸
                int maxSize = Math.Max(mat.Width, mat.Height);
                double scale = (double)size / maxSize;
                int newWidth = (int)(mat.Width * scale);
                int newHeight = (int)(mat.Height * scale);

                using var resized = mat.Resize(new Size(newWidth, newHeight), 0, 0, InterpolationFlags.Linear);
                var thumbnail = MatToBitmapSource(resized);

                // 冻结以提高性能
                thumbnail.Freeze();

                lock (_lock)
                {
                    _thumbnail = thumbnail;
                    _thumbnailState = ImageLoadState.Loaded;
                }

                sw.Stop();
                OnImageLoaded(ImageFormat.Thumbnail, true, null, sw.ElapsedMilliseconds);

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

                if (_fullImageState == ImageLoadState.Loading)
                    return null;

                _fullImageState = ImageLoadState.Loading;
            }

            try
            {
                var sw = Stopwatch.StartNew();
                using var mat = Cv2.ImRead(_filePath, ImreadModes.Color);
                if (mat == null || mat.Empty())
                {
                    OnImageLoaded(ImageFormat.FullImage, false, "无法读取图像文件", 0);
                    return null;
                }

                var fullImage = MatToBitmapSource(mat);
                fullImage.Freeze();

                lock (_lock)
                {
                    _fullImage = fullImage;
                    _fullImageState = ImageLoadState.Loaded;
                }

                sw.Stop();
                OnImageLoaded(ImageFormat.FullImage, true, null, sw.ElapsedMilliseconds);

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

                if (_matState == ImageLoadState.Loaded && _visionImage != null)
                    return _visionImage;

                if (_matState == ImageLoadState.Loading)
                    return null;

                _matState = ImageLoadState.Loading;
            }

            try
            {
                var sw = Stopwatch.StartNew();
                var mat = Cv2.ImRead(_filePath, ImreadModes.Unchanged);
                if (mat == null || mat.Empty())
                {
                    OnImageLoaded(ImageFormat.Mat, false, "无法读取图像文件", 0);
                    return null;
                }

                var pixelData = new byte[mat.Total() * mat.ElemSize()];
                System.Runtime.InteropServices.Marshal.Copy(mat.Data, pixelData, 0, pixelData.Length);

                lock (_lock)
                {
                    _mat = mat;
                    _matState = ImageLoadState.Loaded;
                    var visionImage = VisionAdapter.Factory.LoadFromBytes(pixelData, mat.Width, mat.Height, mat.Channels());
                    _visionImage = visionImage;
                }

                sw.Stop();
                OnImageLoaded(ImageFormat.Mat, true, null, sw.ElapsedMilliseconds);

                return _visionImage;
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _matState = ImageLoadState.Failed;
                }
                OnImageLoaded(ImageFormat.Mat, false, ex.Message, 0);
                return null;
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
        /// 预加载指定格式
        /// </summary>
        public void Preload(ImageFormat formats, CancellationToken cancellationToken = default)
        {
            if (formats.HasFlag(ImageFormat.Thumbnail))
            {
                Task.Run(() => GetThumbnail(_thumbnailSize), cancellationToken);
            }

            if (formats.HasFlag(ImageFormat.FullImage))
            {
                Task.Run(GetFullImage, cancellationToken);
            }

            if (formats.HasFlag(ImageFormat.Mat))
            {
                Task.Run(GetMat, cancellationToken);
            }
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

            if (formats.HasFlag(ImageFormat.Mat))
                tasks.Add(GetMatAsync(cancellationToken));

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

                if (formats.HasFlag(ImageFormat.Mat))
                {
                    _mat?.Dispose();
                    _mat = null;
                    _visionImage?.Dispose();
                    _visionImage = null;
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
        /// 抛出已释放异常
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileImageSource));
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
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;

                _thumbnail = null;
                _fullImage = null;
                _mat?.Dispose();
                _mat = null;
                _visionImage?.Dispose();
                _visionImage = null;

                _disposed = true;
            }
        }
    }
}
