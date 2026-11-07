using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Foundation.Logging;
using SunEyeVision.UI.Services.Thumbnail;

namespace SunEyeVision.UI.Shared
{
    /// <summary>
    /// 图像运行模式枚举
    /// </summary>
    public enum ImageRunMode
    {
        运行全部 = 0,
        运行选择 = 1
    }

    /// <summary>
    /// 缩略图加载状态（驱动 DataTemplate 三态反馈）
    /// </summary>
    public enum ThumbnailState
    {
        Pending = 0,   // 尚未请求解码
        Loading = 1,   // 后台解码中
        Loaded = 2,    // 解码成功，已绑定 Thumbnail
        Failed = 3     // 解码失败，显示占位
    }

    /// <summary>
    /// 图像信息项
    /// </summary>
    public class ImageInfo : INotifyPropertyChanged
    {
        public string? SourceReference { get; init; }
        public string? ResourceError { get; init; }
        private int _displayIndex;
        private bool _isSelected;
        private bool _isForRun = false;
        private WeakReference<BitmapSource>? _fullImage;
        private string? _fullImageVersion;
        private bool _isFullImageLoaded;
        private System.Threading.CancellationTokenSource? _thumbnailCts;
        private ThumbnailState _thumbnailState = ThumbnailState.Pending;
        private DateTime _lastThumbnailRequestUtc;

        /// <summary>
        /// 缩略图加载状态（驱动 DataTemplate 三态反馈：加载中/成功/失败）
        /// </summary>
        public ThumbnailState ThumbnailState
        {
            get => _thumbnailState;
            private set
            {
                if (_thumbnailState != value)
                {
                    _thumbnailState = value;
                    OnPropertyChanged(nameof(ThumbnailState));
                }
            }
        }

        public string Id { get; init; } = Guid.NewGuid().ToString();
        public string Name { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (_thumbnail != value)
                {
                    _thumbnail = value;
                    OnPropertyChanged(nameof(Thumbnail));
                }
            }
        }
        private BitmapSource? _thumbnail;
        public DateTime AddedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 懒加载的全分辨率图像，只在首次访问时加载
        /// </summary>
        public BitmapSource? FullImage
        {
            get
            {
                if (!File.Exists(FilePath)) { _fullImage = null; _isFullImageLoaded = false; return null; }
                string version;
                try { version = ImageCache.ReadVersion(FilePath); }
                catch (IOException) { return null; }
                catch (UnauthorizedAccessException) { return null; }
                if (_fullImageVersion == version && _fullImage != null && _fullImage.TryGetTarget(out var retained)) return retained;
                var image = LocalImageAssetCache.LoadImageOptimized(FilePath);
                _fullImage = image == null ? null : new(image);
                _fullImageVersion = version;
                _isFullImageLoaded = image != null;
                return image;
            }
            set
            {
                if (!(_fullImage != null && _fullImage.TryGetTarget(out var old) && ReferenceEquals(old, value)))
                {
                    _fullImage = value == null ? null : new(value);
                    _fullImageVersion = File.Exists(FilePath) ? ImageCache.ReadVersion(FilePath) : null;
                    _isFullImageLoaded = (value != null);
                    OnPropertyChanged(nameof(FullImage));
                }
            }
        }

        /// <summary>
        /// 是否已加载全分辨率图像
        /// </summary>
        public bool IsMissing => !File.Exists(FilePath);
        public string FileStatus => ResourceError ?? (IsMissing ? "图像文件不存在：" + (SourceReference ?? FilePath) : FilePath);

        public bool IsFullImageLoaded => _isFullImageLoaded && _fullImage?.TryGetTarget(out _) == true;

        public int DisplayIndex
        {
            get => _displayIndex;
            set
            {
                if (_displayIndex != value)
                {
                    _displayIndex = value;
                    OnPropertyChanged(nameof(DisplayIndex));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        /// <summary>
        /// 是否被选中用于运行（运行选择模式时使用）
        /// </summary>
        public bool IsForRun
        {
            get => _isForRun;
            set
            {
                if (_isForRun != value)
                {
                    if (RequestRunSelection is {} request) { request(value); return; }
                    _isForRun = value;
                    OnPropertyChanged(nameof(IsForRun));
                }
            }
        }

        internal Action<bool>? RequestRunSelection { get; set; }
        internal void SetRunSelectionRaw(bool value) => _isForRun = value;
        internal void NotifyRunSelection() => OnPropertyChanged(nameof(IsForRun));

        /// <summary>
        /// 异步加载缩略图（后台线程 WIC 解码 + Freeze + 回 UI 线程更新 Thumbnail）。
        /// 缓存命中秒出，未命中后台解码不阻塞 UI。
        /// 幂等：Thumbnail 已完成 或 正在 Loading 中 都直接返回（不取消重建，防高频刷新抖动）。
        /// 仅 Pending / Failed 触发：滚动离开时 CancelThumbnail() 取消，滚动回来由 RefreshVisibleThumbnails 重新触发（对齐 ImageGlass 主动驱动）。
        /// 失败重置状态（Pending），滚动回来允许重试，避免"永远空白"（rule-015 失败可见+可重试）。
        /// </summary>
        public void EnsureThumbnail()
        {
            if (Thumbnail != null && DateTime.UtcNow - _lastThumbnailRequestUtc < TimeSpan.FromMilliseconds(250)) return;
            if (_thumbnailState == ThumbnailState.Loading) return;   // 进行中不重复触发（防抖动）

            var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.FromThread(System.Threading.Thread.CurrentThread);
            if (dispatcher == null) return;
            _lastThumbnailRequestUtc = DateTime.UtcNow;

            // Pending / Failed → 触发解码；取消上一次仍在进行的解码（如有残留）
            _thumbnailCts?.Cancel();
            _thumbnailCts?.Dispose();
            _thumbnailCts = new System.Threading.CancellationTokenSource();
            var token = _thumbnailCts.Token;

            ThumbnailState = ThumbnailState.Loading;

            var path = FilePath;
            _ = LoadThumbnailForRequestAsync(path, token, dispatcher);
        }

        private async System.Threading.Tasks.Task LoadThumbnailForRequestAsync(string path,
            System.Threading.CancellationToken token, Dispatcher dispatcher)
        {
            try
            {
                if (token.IsCancellationRequested) return;
                var thumbnail = await LocalImageAssetCache.LoadThumbnailAsync(path, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;
                await dispatcher.InvokeAsync(() =>
                {
                    // 若已滚动离开（被取消），丢弃结果
                    if (token.IsCancellationRequested || FilePath != path) return;
                    if (thumbnail != null)
                    {
                        Thumbnail = thumbnail;
                        ThumbnailState = ThumbnailState.Loaded;
                    }
                    else
                    {
                        // 解码失败：标记 Failed 显示占位。滚动离开再回来时 OnItemLoaded 会重新触发解码（rule-015 失败可见+可重试）
                        ThumbnailState = ThumbnailState.Failed;
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Warning, $"缩略图加载失败 | {path} | {ex.Message}", "ImageInfo");
                if (!token.IsCancellationRequested && !dispatcher.HasShutdownStarted)
                    await dispatcher.InvokeAsync(() => { if (!token.IsCancellationRequested) ThumbnailState = ThumbnailState.Failed; });
            }
        }

        /// <summary>
        /// 取消进行中的缩略图解码（滚动离开时调用，对齐 ImageGlass GalleryControl 不可见即取消）。
        /// </summary>
        public void CancelThumbnail()
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts?.Dispose();
            _thumbnailCts = null;
            Thumbnail = null;
            ThumbnailState = ThumbnailState.Pending;
        }

        /// <summary>
        /// 手动设置已加载的全分辨率图像（用于异步加载完成后的更新）
        /// </summary>
        public void SetFullImage(BitmapSource? image)
        {
            _fullImage = image == null ? null : new(image);
            _fullImageVersion = File.Exists(FilePath) ? ImageCache.ReadVersion(FilePath) : null;
            _isFullImageLoaded = (image != null);
            OnPropertyChanged(nameof(FullImage));
        }

        /// <summary>
        /// 释放全分辨率图像以节省内存
        /// </summary>
        public void ReleaseFullImage()
        {
            bool wasLoaded = (_fullImage != null);
            _fullImage = null;
            _isFullImageLoaded = false;

            if (wasLoaded)
            {
                OnPropertyChanged(nameof(FullImage));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
