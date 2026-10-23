using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Shell.Controls.Panels
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
        public string? SourceReference { get; set; }
        public string? ResourceError { get; set; }
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

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
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
                var image = ImagePreviewControl.LoadImageOptimized(FilePath);
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
                    _isForRun = value;
                    OnPropertyChanged(nameof(IsForRun));
                }
            }
        }

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
                var thumbnail = await ImagePreviewControl.LoadThumbnailAsync(path, token).ConfigureAwait(false);
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

    /// <summary>
    /// LRU图像缓存（全分辨率图像，上限 30）
    /// </summary>
    public class ImageCache
    {
        private readonly int _maxCacheSize;
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cacheMap;
        private readonly LinkedList<CacheEntry> _lruList;
        private readonly long _byteBudget;
        private long _bytes;
        public long DecodedBytes { get { lock (_cacheMap) return _bytes; } }

        public ImageCache(int maxCacheSize = 30, long byteBudget = 64 * 1024 * 1024)
        {
            if (maxCacheSize <= 0 || byteBudget <= 0) throw new ArgumentOutOfRangeException(nameof(maxCacheSize));
            _maxCacheSize = maxCacheSize;
            _byteBudget = byteBudget;
            _cacheMap = new Dictionary<string, LinkedListNode<CacheEntry>>(StringComparer.OrdinalIgnoreCase);
            _lruList = new LinkedList<CacheEntry>();
        }

        internal static string ReadVersion(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        }

        /// <summary>
        /// 查询缓存（命中则提升 LRU 优先级）。调用方须在外部加锁（仅保护字典，短临界区）。
        /// </summary>
        public BitmapSource? Get(string filePath, string? version = null)
        {
            version ??= ReadVersion(filePath);
            lock (_cacheMap)
            {
            if (_cacheMap.TryGetValue(filePath, out var node))
            {
                if (node.Value.Version != version) { Remove(filePath); return null; }
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.Bitmap;
            }
            return null;
            }
        }

        /// <summary>
        /// 写入缓存（含 LRU 淘汰）。调用方须在外部加锁。已在缓存中则不重复写入。
        /// </summary>
        public void Set(string filePath, BitmapSource? bitmap, string? version = null)
        {
            if (bitmap == null) return;
            version ??= ReadVersion(filePath);
            lock (_cacheMap)
            {
            Remove(filePath);

            var entry = new CacheEntry(filePath, bitmap, version);
            if (entry.Bytes > _byteBudget) return;
            var newNode = _lruList.AddFirst(entry);
            _cacheMap[filePath] = newNode;
            _bytes += entry.Bytes;

            while (_lruList.Count > _maxCacheSize || _bytes > _byteBudget)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _cacheMap.Remove(lastNode.Value.FilePath);
                    _bytes -= lastNode.Value.Bytes;
                    _lruList.RemoveLast();
                }
            }
            }
        }

        public void Remove(string path)
        {
            lock (_cacheMap)
                if (_cacheMap.Remove(path, out var node)) { _bytes -= node.Value.Bytes; _lruList.Remove(node); }
        }

        public void Clear()
        {
            lock (_cacheMap)
            {
                _cacheMap.Clear();
                _lruList.Clear();
                _bytes = 0;
            }
        }

        private class CacheEntry
        {
            public string FilePath { get; }
            public BitmapSource Bitmap { get; }
            public string Version { get; }
            public long Bytes { get; }

            public CacheEntry(string filePath, BitmapSource bitmap, string version)
            {
                FilePath = filePath;
                Bitmap = bitmap;
                Version = version;
                Bytes = ((long)bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8 * bitmap.PixelHeight;
            }
        }
    }

    /// <summary>
    /// 图像预览控件
    /// </summary>
    /// <remarks>
    /// 缩略图架构（2026-08-18 治本）：
    /// - ImageInfo.EnsureThumbnail：后台线程 WIC 解码（DecodePixelWidth=100 + Freeze）+ 静态 LRU 缓存，
    ///   回 UI 线程绑定 Thumbnail；滚动时缓存命中秒出，未命中后台解码不阻塞 UI。
    /// - 已清理自研加载/缓存/调度死代码（PriorityThumbnailLoader/SmartThumbnailLoader/WicGpuDecoder/
    ///   ImageSharpDecoder/ThumbnailCacheManager/MemoryPressureMonitor/FilePathToImageSourceConverter）。
    /// - 保留对外契约：4 个依赖属性、WorkflowExecutionRequested 事件、4 个命令、ImageCountDisplay。
    /// - 保留 ImageInfo/ImageCache/ImageRunMode（供外部引用），保留 LoadImageOptimized（全图懒加载）。
    /// </remarks>
    public partial class ImagePreviewControl : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        // ListBox 控件引用（用于同步选中项）
        private ListBox? _thumbnailListBox;
        private ImageInfo? _highlightedImage;
        private readonly HashSet<ImageInfo> _realizedThumbnails = new();
        private const int MaximumRealizedThumbnails = 128;

        // 防止 CurrentImageIndex DP ↔ ListBox.SelectedItem 循环回调
        private bool _isSyncingListBox;

        // 主动驱动刷新：滚动停止后 Debounce 触发（对齐 ImageGlass RefreshRealizedThumbnails）
        private readonly System.Windows.Threading.DispatcherTimer _refreshDebounceTimer;

        public static readonly DependencyProperty AutoSwitchEnabledProperty =
            DependencyProperty.Register("AutoSwitchEnabled", typeof(bool), typeof(ImagePreviewControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty CurrentImageIndexProperty =
            DependencyProperty.Register("CurrentImageIndex", typeof(int), typeof(ImagePreviewControl),
                new PropertyMetadata(-1, OnCurrentImageIndexChanged));

        public static readonly DependencyProperty ImageCollectionProperty =
            DependencyProperty.Register("ImageCollection", typeof(BatchObservableCollection<ImageInfo>), typeof(ImagePreviewControl),
                new PropertyMetadata(null, OnImageCollectionChanged));

        /// <summary>
        /// 绑定的活动输入源（真相源）。AppendFiles 直接写 ImageInputSource._images，
        /// 不依赖 ImageCollection 绑定回写（Images 是只读属性，TwoWay 绑定无法建立）。
        /// </summary>
        public static readonly DependencyProperty ActiveInputSourceProperty =
            DependencyProperty.Register(nameof(ActiveInputSource), typeof(ImageInputSource), typeof(ImagePreviewControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ImageRunModeProperty =
            DependencyProperty.Register("ImageRunMode", typeof(ImageRunMode), typeof(ImagePreviewControl),
                new PropertyMetadata(ImageRunMode.运行全部, OnImageRunModeChanged));

        /// <summary>
        /// 是否启用自动切换
        /// </summary>
        public bool AutoSwitchEnabled
        {
            get => (bool)GetValue(AutoSwitchEnabledProperty);
            set => SetCurrentValue(AutoSwitchEnabledProperty, value);
        }

        /// <summary>
        /// 当前显示的图像索引
        /// </summary>
        public int CurrentImageIndex
        {
            get => (int)GetValue(CurrentImageIndexProperty);
            set => SetCurrentValue(CurrentImageIndexProperty, value);
        }

        /// <summary>
        /// 图像集合（使用批量操作优化集合）
        /// </summary>
        public BatchObservableCollection<ImageInfo> ImageCollection
        {
            get => (BatchObservableCollection<ImageInfo>)GetValue(ImageCollectionProperty);
            set => SetValue(ImageCollectionProperty, value);
        }

        /// <summary>
        /// 绑定的活动输入源（真相源，添加图像时直接写回 ImageInputSource._images）
        /// </summary>
        public ImageInputSource? ActiveInputSource
        {
            get => (ImageInputSource?)GetValue(ActiveInputSourceProperty);
            set => SetValue(ActiveInputSourceProperty, value);
        }

        /// <summary>
        /// 图像运行模式
        /// </summary>
        public ImageRunMode ImageRunMode
        {
            get => (ImageRunMode)GetValue(ImageRunModeProperty);
            set => SetCurrentValue(ImageRunModeProperty, value);
        }

        /// <summary>
        /// 工作流执行请求事件 - 当用户点击图片请求执行工作流时触发
        /// </summary>
        public event EventHandler<WorkflowExecutionRequestEventArgs>? WorkflowExecutionRequested;

        /// <summary>
        /// 图像计数显示文本
        /// </summary>
        public string ImageCountDisplay
        {
            get
            {
                int count = ImageCollection?.Count ?? 0;
                int current = CurrentImageIndex >= 0 && CurrentImageIndex < count ? CurrentImageIndex + 1 : 0;
                return count > 0 ? $"图像源 ({current}/{count})" : "图像源";
            }
        }

        public ICommand AddImageCommand { get; }
        public ICommand AddFolderCommand { get; }
        public ICommand DeleteSingleImageCommand { get; }
        public ICommand ClearAllCommand { get; }

        // 全分辨率图像缓存（LRU，上限 30）——保留，供 ImageInfo.FullImage 懒加载
        private static readonly ImageCache s_fullImageCache = new ImageCache(maxCacheSize: 30);

        private static readonly PreviewBitmapCache s_thumbnailCache = new(DecodeThumbnailCore);
        public static PreviewBitmapCacheStats ThumbnailCacheStatistics => s_thumbnailCache.Statistics;

        internal static System.Threading.Tasks.Task<BitmapSource?> LoadThumbnailAsync(string filePath,
            System.Threading.CancellationToken token = default) => s_thumbnailCache.LoadAsync(filePath, token);

        internal static BitmapImage? LoadThumbnail(string filePath, System.Threading.CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;
            try { return LoadThumbnailAsync(filePath, token).GetAwaiter().GetResult() as BitmapImage; }
            catch (OperationCanceledException) { return null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static BitmapSource DecodeThumbnailCore(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var header = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var landscape = header.PixelWidth >= header.PixelHeight;
            stream.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            if (landscape) bitmap.DecodePixelWidth = 100;
            else bitmap.DecodePixelHeight = 100;
            // Fresh streams have no URI cache key. Content versions are owned by PreviewBitmapCache.
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        /// <summary>
        /// 全分辨率图像加载（简化版）：ImageInfo.FullImage 懒加载入口。
        /// 用新文件流 + OnLoad 解码，版本由内容哈希校验。
        /// </summary>
        public static BitmapImage? LoadImageOptimized(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            // The legacy display caller schedules this file/decoder work off the dispatcher.
            try
            {
                var version = ImageCache.ReadVersion(filePath);
                var cached = s_fullImageCache.Get(filePath, version);
                if (cached != null) return cached as BitmapImage;
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                if (ImageCache.ReadVersion(filePath) != version) return null;
                s_fullImageCache.Set(filePath, bitmap, version);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public ImagePreviewControl()
        {
            InitializeComponent();
            ImageCollection = new BatchObservableCollection<ImageInfo>();

            AddImageCommand = new RelayCommand(ExecuteAddImage);
            AddFolderCommand = new RelayCommand(ExecuteAddFolder);
            DeleteSingleImageCommand = new RelayCommand<ImageInfo>(ExecuteDeleteSingleImage);
            ClearAllCommand = new RelayCommand(ExecuteClearAll, CanExecuteClearAll);
            CancelImportCommand = new RelayCommand(CancelFileImport, () => IsImporting);

            ImageCollection.CollectionChanged += (s, e) => OnPropertyChanged(nameof(ImageCountDisplay));
            ImageCollection.CollectionChanged += (s, e) => ScheduleRefreshVisibleThumbnails();

            // 滚动停止后 100ms 触发主动刷新（Debounce，避免拖动中高频遍历）
            _refreshDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _refreshDebounceTimer.Tick += (s, e) =>
            {
                _refreshDebounceTimer.Stop();
                RefreshVisibleThumbnails();
            };

            Loaded += OnControlLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += (s, e) => ScheduleRefreshVisibleThumbnails();
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            // 延迟初始化，确保 ListBox 已完全加载
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _thumbnailListBox = FindName("ThumbnailListBox") as ListBox;
                if (_thumbnailListBox != null)
                {
                    // 运行选择模式下方向键只在已勾选（应运行）图之间导航
                    _thumbnailListBox.PreviewKeyDown -= OnThumbnailPreviewKeyDown;
                    _thumbnailListBox.PreviewKeyDown += OnThumbnailPreviewKeyDown;
                }
                // 控件加载后立即触发一次主动刷新，保证初始可见项都被解码（主动驱动兜底 Loaded 事件）
                RefreshVisibleThumbnails();
            }), DispatcherPriority.Loaded);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelFileImport();
            _refreshDebounceTimer.Stop();
            if (_thumbnailListBox != null) _thumbnailListBox.PreviewKeyDown -= OnThumbnailPreviewKeyDown;
            _thumbnailListBox = null;
            // Unloading a view must not clear the persisted input source.
            if (ImageCollection != null) foreach (var image in ImageCollection) image.CancelThumbnail();
            _realizedThumbnails.Clear();
        }

        #region 命令实现

        private async void ExecuteAddImage()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图像文件|*.jpg;*.jpeg;*.png;*.bmp;*.tiff|所有文件|*.*",
                    Title = "选择图像文件",
                    Multiselect = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    await ImportFilePathsAsync(openFileDialog.FileNames);
                }
            }
            catch (Exception ex)
            {
                ThemeMessageDialog.Show($"添加图像失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            }
        }

        private async void ExecuteAddFolder()
        {
            try
            {
                var folderDialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "选择图像文件夹"
                };

                if (folderDialog.ShowDialog() == true)
                {
                    await ImportDirectoryAsync(folderDialog.FolderName);
                }
            }
            catch (Exception ex)
            {
                ThemeMessageDialog.Show($"添加文件夹失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            }
        }

        /// <summary>
        /// 虚拟化容器被物化（进入可视区域）时触发缩略图解码。
        /// 业界成熟做法：由虚拟化系统决定"哪些项可见"，容器 Loaded 即代表即将显示，
        /// 无需手算视口/索引，对项宽、DPI、滚动位置变化完全免疫。
        /// </summary>
        private void OnItemLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageInfo info)
            {
                if (ImageCollection?.Contains(info) != true) return;
                if (_realizedThumbnails.Contains(info) || _realizedThumbnails.Count < MaximumRealizedThumbnails)
                {
                    _realizedThumbnails.Add(info);
                    info.EnsureThumbnail();
                }
            }
        }

        /// <summary>
        /// 虚拟化容器滚出可视区域（被回收）时取消进行中的缩略图解码。
        /// 对齐 ImageGlass GalleryControl：不可见即取消，省 CPU 给当前视口。
        /// 滚动回来时 OnItemLoaded 会用新 token 重新触发解码，旧任务的结果被丢弃。
        /// </summary>
        private void OnItemUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageInfo info)
            {
                _realizedThumbnails.Remove(info);
                info.CancelThumbnail();
            }
        }

        private void ExecuteDeleteSingleImage(ImageInfo? imageInfo)
        {
            if (imageInfo == null) return;
            CancelFileImport();

            // ★ 走真相源 ActiveInputSource.RemoveImage：改 ImageInputSource._images（与 ImageCollection 同实例，
            // OneWay 绑定自动刷新 UI），并触发 ConfigChanged 实时写回节点 ImageSourceConfig。
            var source = ActiveInputSource;
            if (source == null) return;

            int index = source.Images.IndexOf(imageInfo);
            if (index < 0) return;

            source.RemoveImage(imageInfo);

            // The truth source already adjusted the index; do not decrement twice.
            CurrentImageIndex = source.CurrentIndex;

            RefreshImageDisplayState();
        }

        private void ExecuteClearAll()
        {
            CancelFileImport();
            if (!CanExecuteClearAll()) return;
            if (ThemeMessageDialog.Show("清空所有图像引用？不会删除磁盘文件。", "清空图像",
                MessageBoxButton.YesNo, ThemeMessageDialog.MessageIcon.Question) == MessageBoxResult.Yes)
                ClearInputReferences();
        }
        public void ClearInputReferences()
        {
            if (ActiveInputSource != null) ActiveInputSource.ClearImages();
            else
            {
                foreach (var image in ImageCollection) { image.CancelThumbnail(); image.ReleaseFullImage(); InvalidateImageCache(image.FilePath); }
                ImageCollection.Clear();
            }
            SetCurrentValue(CurrentImageIndexProperty, -1);
            RefreshImageDisplayState();
            CommandManager.InvalidateRequerySuggested();
        }
        public static void InvalidateImageCache(string path)
        {
            s_fullImageCache.Remove(path);
            s_thumbnailCache.Invalidate(path);
        }

        private bool CanExecuteClearAll() => (ActiveInputSource?.Count ?? ImageCollection?.Count ?? 0) > 0 || CurrentImageIndex >= 0;

        #endregion

        #region 辅助方法

        private static void OnCurrentImageIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
        {
            var control = (ImagePreviewControl)d;
            control.UpdateImageSelection();
            control.OnPropertyChanged(nameof(ImageCountDisplay));

            if (control._thumbnailListBox != null && control.ImageCollection != null &&
                control.CurrentImageIndex >= 0 && control.CurrentImageIndex < control.ImageCollection.Count)
            {
                control._isSyncingListBox = true;
                control._thumbnailListBox.SelectedItem = control.ImageCollection[control.CurrentImageIndex];
                control._isSyncingListBox = false;
            }
        }

        private static void OnImageCollectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ImagePreviewControl)d;
            control.CancelFileImport();
            if (control._highlightedImage != null) control._highlightedImage.IsSelected = false;
            control._highlightedImage = null;
            if (e.OldValue is BatchObservableCollection<ImageInfo> oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnBoundCollectionChanged;
                foreach (var image in oldCollection) image.CancelThumbnail();
                control._realizedThumbnails.Clear();
            }
            if (e.NewValue is BatchObservableCollection<ImageInfo> newCollection)
            {
                newCollection.CollectionChanged += control.OnBoundCollectionChanged;
                foreach (var image in newCollection) image.IsSelected = false;
            }
            control.UpdateImageSelection();
            control.OnPropertyChanged(nameof(ImageCountDisplay));
            CommandManager.InvalidateRequerySuggested();
        }
        private void OnBoundCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ImageCountDisplay));
            CommandManager.InvalidateRequerySuggested();
            RefreshImageDisplayState();
        }

        private static void OnImageRunModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
        {
            var control = (ImagePreviewControl)d;
            control.OnPropertyChanged(nameof(ImageRunMode));
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// 刷新图像显示状态（删除图片后同步选中项与计数）
        /// </summary>
        private void RefreshImageDisplayState()
        {
            OnPropertyChanged(nameof(ImageCountDisplay));
            UpdateImageSelection();

            if (_thumbnailListBox != null)
            {
                if (CurrentImageIndex >= 0 && ImageCollection != null && CurrentImageIndex < ImageCollection.Count)
                {
                    _isSyncingListBox = true;
                    _thumbnailListBox.SelectedItem = ImageCollection[CurrentImageIndex];
                    _isSyncingListBox = false;
                }
                else
                {
                    _thumbnailListBox.SelectedItem = null;
                }
            }
        }

        /// <summary>
        /// 更新图像选中状态（增量更新，仅变更新旧两项）
        /// </summary>
        private void UpdateImageSelection()
        {
            var next = ImageCollection != null && CurrentImageIndex >= 0 && CurrentImageIndex < ImageCollection.Count
                ? ImageCollection[CurrentImageIndex] : null;
            if (ReferenceEquals(next, _highlightedImage)) return;
            if (_highlightedImage != null) _highlightedImage.IsSelected = false;
            _highlightedImage = next;
            if (next != null) next.IsSelected = true;
        }

        #region 自动切换（流程驱动：宿主在工作流执行完成后调用推进，非固定间隔）

        /// <summary>计算"应运行"图片子集：运行全部=所有图；运行选择=仅勾选 IsForRun</summary>
        private List<ImageInfo> GetRunnableImages()
        {
            if (ImageCollection == null || ImageCollection.Count == 0)
                return new List<ImageInfo>();
            if (ImageRunMode == ImageRunMode.运行选择)
                return ImageCollection.Where(i => i.IsForRun && File.Exists(i.FilePath)).ToList();
            return ImageCollection.Where(i => File.Exists(i.FilePath)).ToList();
        }

        /// <summary>
        /// 推进到"当前图之后"的第一张应运行图并同步索引（不触发执行），**末尾循环回第一张**。
        /// 由宿主在每轮工作流执行开始前调用（单次执行点执行 / 连续执行每轮），
        /// 输出下一张应运行图供宿主注入运行时参数。仅当应运行图不足 2 张时返回 false（无实质切换）。
        /// </summary>
        public bool TryStepNextImageForAuto(out ImageInfo? nextImage)
        {
            nextImage = null;
            if (!AutoSwitchEnabled) return false;

            var runnables = GetRunnableImages();
            if (runnables.Count == 0) return false;

            int curIndex = CurrentImageIndex;
            int pos = -1;
            for (int i = 0; i < runnables.Count; i++)
            {
                if (ImageCollection.IndexOf(runnables[i]) == curIndex)
                {
                    pos = i;
                    break;
                }
            }

            // 循环推进：当前之后一张，到末尾则回绕到第一张（所有图片执行完从头再开始）
            int next = pos < 0 ? 0 : (pos + 1) % runnables.Count;
            if (next == pos)
                return false;   // 只有一张应运行图：无实质切换，沿用当前

            var target = runnables[next];
            int targetIndex = ImageCollection.IndexOf(target);
            if (targetIndex < 0) return false;

            CurrentImageIndex = targetIndex;
            nextImage = target;
            return true;
        }

        #endregion

        #endregion

        #region 交互事件

        /// <summary>
        /// 运行选择模式：方向键只在"勾选（IsForRun）应运行图"之间导航，跳过未勾选图。
        /// 所有模式均从明确的键盘入口请求执行，选中变化本身不触发执行。
        /// </summary>
        private void OnThumbnailPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_thumbnailListBox == null) return;
            for (var source = e.OriginalSource as DependencyObject; source != null;)
            {
                if (source is System.Windows.Controls.Primitives.ButtonBase) return;
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }

            bool goNext = e.Key == Key.Right || e.Key == Key.Down;
            bool goPrev = e.Key == Key.Left || e.Key == Key.Up;
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                if (_thumbnailListBox.SelectedItem is ImageInfo selected &&
                    (ImageRunMode != ImageRunMode.运行选择 || selected.IsForRun))
                    RequestWorkflowExecution(selected, ImageCollection.IndexOf(selected));
                e.Handled = true; return;
            }
            if (!goNext && !goPrev && e.Key != Key.Home && e.Key != Key.End) return;

            var runnables = GetRunnableImages();
            if (runnables.Count == 0) return;

            int curIndex = CurrentImageIndex;
            int pos = -1;
            for (int i = 0; i < runnables.Count; i++)
            {
                if (ImageCollection.IndexOf(runnables[i]) == curIndex)
                {
                    pos = i;
                    break;
                }
            }

            int target;
            if (e.Key == Key.Home) target = 0;
            else if (e.Key == Key.End) target = runnables.Count - 1;
            else if (pos < 0) target = goNext ? 0 : -1;
            else target = goNext ? pos + 1 : pos - 1;

            if (target < 0 || target >= runnables.Count)
            {
                // 已在边界：吃掉按键，避免高亮/选中移到未勾选图
                e.Handled = true;
                return;
            }

            var targetImage = runnables[target];
            e.Handled = true;
            _thumbnailListBox.SelectedItem = targetImage;
            _thumbnailListBox.ScrollIntoView(targetImage);
            RequestWorkflowExecution(targetImage, ImageCollection.IndexOf(targetImage));
        }

        private void OnThumbnailRemoveReference(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { DataContext: ImageInfo image }) ExecuteDeleteSingleImage(image);
        }

        private void OnThumbnailClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            // 如果点击的是删除按钮或 CheckBox，跳过处理
            DependencyObject? current = e.OriginalSource as DependencyObject;
            while (current != null)
            {
                if (current is Button || current is CheckBox)
                    return;
                current = VisualTreeHelper.GetParent(current);
            }

            if (sender is Border border && border.Tag is ImageInfo imageInfo)
            {
                int index = ImageCollection.IndexOf(imageInfo);
                if (index >= 0)
                {
                    // 运行选择模式：只有已勾选的图片才执行工作流；运行全部模式：点击任意图片执行
                    if (ImageRunMode == ImageRunMode.运行选择 && !imageInfo.IsForRun)
                        return;

                    RequestWorkflowExecution(imageInfo, index);
                }
            }
        }

        private void RequestWorkflowExecution(ImageInfo imageInfo, int index)
        {
            if (index < 0 || !File.Exists(imageInfo.FilePath)) return;
            CurrentImageIndex = index;
            WorkflowExecutionRequested?.Invoke(this, new WorkflowExecutionRequestEventArgs
            {
                ImageInfo = imageInfo,
                Index = index
            });
        }

        private void OnThumbnailListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingListBox) return;

            if (e.AddedItems.Count == 0 || e.AddedItems[0] is not ImageInfo selectedImage)
                return;

            int index = ImageCollection?.IndexOf(selectedImage) ?? -1;
            if (index < 0) return;

            // Selection may come from right-click, binding, restore or keyboard.
            // Only explicit left-click/key handlers may request execution.
            CurrentImageIndex = index;
        }

        private void OnRunModeButtonClick(object sender, RoutedEventArgs e)
        {
            if (RunModePopup != null)
            {
                RunModePopup.IsOpen = !RunModePopup.IsOpen;
            }
        }

        private void OnRunModeListBoxPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (RunModeListBox == null || RunModePopup == null) return;

            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null) return;

            var listBoxItem = FindParent<ListBoxItem>(originalSource);
            if (listBoxItem == null) return;

            ImageRunMode? selectedMode = null;
            if (listBoxItem.Content is TextBlock textBlock && textBlock.DataContext is ImageRunMode mode1)
            {
                selectedMode = mode1;
            }
            else if (listBoxItem.DataContext is ImageRunMode mode2)
            {
                selectedMode = mode2;
            }

            if (selectedMode.HasValue)
            {
                ImageRunMode = selectedMode.Value;
            }

            RunModePopup.IsOpen = false;
            e.Handled = true;
        }

        /// <summary>
        /// 滚动变化：Debounce 后主动刷新可见缩略图。
        /// 依赖 Loaded 事件不可靠（虚拟化复用容器时不重新触发），滚动停止后主动遍历物化项保证可见项都被触发。
        /// 对齐 ImageGlass GalleryControl.RefreshRealizedThumbnails 的主动驱动模型。
        /// </summary>
        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ScheduleRefreshVisibleThumbnails();
        }

        /// <summary>
        /// Debounce 调度主动刷新：拖动中高频滚动只重置计时器，滚动停止 100ms 后才真正刷新一次。
        /// </summary>
        private void ScheduleRefreshVisibleThumbnails()
        {
            _refreshDebounceTimer.Stop();
            _refreshDebounceTimer.Start();
        }

        /// <summary>
        /// 主动遍历虚拟化系统当前物化的 ListBoxItem，对可见项幂等触发缩略图解码。
        /// 物化 = 虚拟化系统判定"即将显示"（含可视区 + 缓冲），故无需手算视口像素。
        /// EnsureThumbnail 幂等：已加载 / Loading 中跳过，Pending / Failed 触发。
        /// 对齐 ImageGlass GalleryControl.axaml.cs:246-252 RefreshRealizedThumbnails。
        /// </summary>
        private void RefreshVisibleThumbnails()
        {
            if (_thumbnailListBox == null || ImageCollection == null) return;
            var visible = FindVisualChildren<ListBoxItem>(_thumbnailListBox)
                .Select(item => item.DataContext).OfType<ImageInfo>()
                .Where(info => ImageCollection.Contains(info)).Take(MaximumRealizedThumbnails).ToHashSet();
            foreach (var old in _realizedThumbnails.Where(info => !visible.Contains(info)).ToArray()) old.CancelThumbnail();
            _realizedThumbnails.Clear();
            foreach (var info in visible)
            {
                _realizedThumbnails.Add(info);
                info.EnsureThumbnail();
            }
        }

        /// <summary>
        /// 深度优先遍历可视化树，收集所有 T 类型的子节点。
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) yield return typed;
                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            return parentObject is T parent ? parent : FindParent<T>(parentObject);
        }

        #endregion

        /// <summary>
        /// 属性更改通知
        /// </summary>
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// 工作流执行请求事件参数
    /// </summary>
    public class WorkflowExecutionRequestEventArgs : EventArgs
    {
        public ImageInfo ImageInfo { get; set; } = null!;
        public int Index { get; set; }
    }

    /// <summary>
    /// 图像运行模式显示转换器
    /// </summary>
    public class ImageRunModeDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ImageRunMode runMode)
            {
                return runMode switch
                {
                    ImageRunMode.运行全部 => "运行全部",
                    ImageRunMode.运行选择 => "运行选中",
                    _ => "运行全部"
                };
            }
            return "运行全部";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "运行全部" => ImageRunMode.运行全部,
                    "运行选中" => ImageRunMode.运行选择,
                    _ => ImageRunMode.运行全部
                };
            }
            return ImageRunMode.运行全部;
        }
    }

    /// <summary>
    /// 运行模式到可见性转换器（运行选择模式时显示）
    /// </summary>
    public class RunModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ImageRunMode mode && mode == ImageRunMode.运行选择)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
