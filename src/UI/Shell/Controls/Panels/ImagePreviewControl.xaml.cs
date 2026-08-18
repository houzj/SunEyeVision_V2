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
        private int _displayIndex;
        private bool _isSelected;
        private bool _isForRun = false;
        private BitmapSource? _fullImage;
        private bool _isFullImageLoaded;
        private System.Threading.CancellationTokenSource? _thumbnailCts;
        private ThumbnailState _thumbnailState = ThumbnailState.Pending;

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
                if (!_isFullImageLoaded && _fullImage == null && !string.IsNullOrEmpty(FilePath))
                {
                    _fullImage = ImagePreviewControl.LoadImageOptimized(FilePath);
                    _isFullImageLoaded = true;
                }
                return _fullImage;
            }
            set
            {
                if (_fullImage != value)
                {
                    _fullImage = value;
                    _isFullImageLoaded = (value != null);
                    OnPropertyChanged(nameof(FullImage));
                }
            }
        }

        /// <summary>
        /// 是否已加载全分辨率图像
        /// </summary>
        public bool IsFullImageLoaded => _isFullImageLoaded && _fullImage != null;

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
            if (Thumbnail != null) return;
            if (_thumbnailState == ThumbnailState.Loading) return;   // 进行中不重复触发（防抖动）

            // Pending / Failed → 触发解码；取消上一次仍在进行的解码（如有残留）
            _thumbnailCts?.Cancel();
            _thumbnailCts?.Dispose();
            _thumbnailCts = new System.Threading.CancellationTokenSource();
            var token = _thumbnailCts.Token;

            ThumbnailState = ThumbnailState.Loading;

            var path = FilePath;
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            System.Threading.Tasks.Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;
                var thumbnail = ImagePreviewControl.LoadThumbnail(path, token);
                if (token.IsCancellationRequested) return;
                dispatcher.InvokeAsync(() =>
                {
                    // 若已滚动离开（被取消），丢弃结果
                    if (token.IsCancellationRequested) return;
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
            });
        }

        /// <summary>
        /// 取消进行中的缩略图解码（滚动离开时调用，对齐 ImageGlass GalleryControl 不可见即取消）。
        /// </summary>
        public void CancelThumbnail()
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts?.Dispose();
            _thumbnailCts = null;
        }

        /// <summary>
        /// 手动设置已加载的全分辨率图像（用于异步加载完成后的更新）
        /// </summary>
        public void SetFullImage(BitmapSource? image)
        {
            _fullImage = image;
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

        public ImageCache(int maxCacheSize = 30)
        {
            _maxCacheSize = maxCacheSize;
            _cacheMap = new Dictionary<string, LinkedListNode<CacheEntry>>();
            _lruList = new LinkedList<CacheEntry>();
        }

        /// <summary>
        /// 查询缓存（命中则提升 LRU 优先级）。调用方须在外部加锁（仅保护字典，短临界区）。
        /// </summary>
        public BitmapSource? Get(string filePath)
        {
            if (_cacheMap.TryGetValue(filePath, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.Bitmap;
            }
            return null;
        }

        /// <summary>
        /// 写入缓存（含 LRU 淘汰）。调用方须在外部加锁。已在缓存中则不重复写入。
        /// </summary>
        public void Set(string filePath, BitmapSource? bitmap)
        {
            if (bitmap == null || _cacheMap.ContainsKey(filePath)) return;

            var entry = new CacheEntry(filePath, bitmap);
            var newNode = _lruList.AddFirst(entry);
            _cacheMap[filePath] = newNode;

            if (_lruList.Count > _maxCacheSize)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _cacheMap.Remove(lastNode.Value.FilePath);
                    _lruList.RemoveLast();
                }
            }
        }

        public void Clear()
        {
            _cacheMap.Clear();
            _lruList.Clear();
        }

        private class CacheEntry
        {
            public string FilePath { get; }
            public BitmapSource Bitmap { get; }

            public CacheEntry(string filePath, BitmapSource bitmap)
            {
                FilePath = filePath;
                Bitmap = bitmap;
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

        // 防止 CurrentImageIndex DP ↔ ListBox.SelectedItem 循环回调
        private bool _isSyncingListBox;

        // 主动驱动刷新：滚动停止后 Debounce 触发（对齐 ImageGlass RefreshRealizedThumbnails）
        private readonly System.Windows.Threading.DispatcherTimer _refreshDebounceTimer;

        public static readonly DependencyProperty AutoSwitchEnabledProperty =
            DependencyProperty.Register("AutoSwitchEnabled", typeof(bool), typeof(ImagePreviewControl),
                new PropertyMetadata(true));

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
            set => SetValue(AutoSwitchEnabledProperty, value);
        }

        /// <summary>
        /// 当前显示的图像索引
        /// </summary>
        public int CurrentImageIndex
        {
            get => (int)GetValue(CurrentImageIndexProperty);
            set => SetValue(CurrentImageIndexProperty, value);
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
            set => SetValue(ImageRunModeProperty, value);
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

        // 缩略图缓存（LRU，上限 50）——跨实例共享，滚动复用不重复解码
        private static readonly ImageCache s_thumbnailCache = new ImageCache(maxCacheSize: 50);
        private static readonly object s_thumbnailLock = new object();

        /// <summary>
        /// 全局缩略图并发上限：clamp(CPU-1, 4, 16)。
        /// 对齐 ImageGlass Photo.cs:66-67，避免海量缩略图无限并发挤爆线程池（rule-008 治本）。
        /// </summary>
        private static readonly int s_thumbnailConcurrency =
            Math.Clamp(Environment.ProcessorCount - 1, 4, 16);
        private static readonly SemaphoreSlim s_thumbnailThrottle =
            new(s_thumbnailConcurrency, s_thumbnailConcurrency);

        /// <summary>
        /// 缩略图加载：后台线程 WIC 解码 + DecodePixelWidth 缩小 + Freeze。
        /// 对齐成熟方案（ImageGlass/微软官方）：
        /// - 并发节流：全局信号量 clamp(CPU-1,4,16)，避免线程池饱和也不串行（对齐 ImageGlass Photo.cs:66-67）
        /// - 锁只保护缓存字典（微秒级短临界区），解码在锁外并行（已治本）
        /// - 解码失败直接返回 null + WARN 日志（rule-015 失败可见），不做无效降级——
        ///   实测 System.Drawing 兜底转 PNG 后仍走 BitmapImage(WIC)，救不回 WIC 解不了的格式
        ///   （对齐 ImageGlass 的容错仅靠其真正多引擎，本项目 Win11 标准格式 WIC 全覆盖，无需兜底）
        /// </summary>
        internal static BitmapImage? LoadThumbnail(string filePath, System.Threading.CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            // 快路径：锁内只查缓存（微秒级），命中直接返回
            lock (s_thumbnailLock)
            {
                var cached = s_thumbnailCache.Get(filePath);
                if (cached != null) return cached as BitmapImage;
            }

            // 节流进入：最多 clamp(CPU-1,4,16) 个并发解码，避免线程池饱和
            BitmapImage? bitmap = null;
            try
            {
                s_thumbnailThrottle.Wait(token);
            }
            catch (System.OperationCanceledException)
            {
                // 滚动离开被取消：不降级、不报错，直接丢弃（对齐 ImageGlass 不可见即取消）
                return null;
            }
            try
            {
                // 慢路径：锁外并行解码（I/O + CPU，互不阻塞）
                try
                {
                    bitmap = DecodeThumbnailCore(filePath);   // WIC
                }
                catch (Exception ex)   // WIC 解不了（NotSupportedException / COM 异常等）
                {
                    // rule-015：失败可见，禁止静默吞异常；不做无效降级
                    VisionLogger.Instance.Log(LogLevel.Warning,
                        $"缩略图解码失败 | {filePath} | {ex.GetType().Name}: {ex.Message}",
                        "ImagePreviewControl");
                }
            }
            finally
            {
                s_thumbnailThrottle.Release();
            }

            if (bitmap == null) return null;

            // 写缓存：锁内极短
            lock (s_thumbnailLock)
            {
                s_thumbnailCache.Set(filePath, bitmap);
            }
            return bitmap;
        }

        /// <summary>
        /// WIC 缩略图解码核心（无锁，可在后台线程并行执行）
        /// </summary>
        private static BitmapImage DecodeThumbnailCore(string filePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath, UriKind.RelativeOrAbsolute);
            bitmap.DecodePixelWidth = 100;                // 解码时直接缩小，省内存
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // 立即解码
            bitmap.EndInit();
            bitmap.Freeze();                              // 冻结后可跨线程
            return bitmap;
        }

        /// <summary>
        /// 全分辨率图像加载（简化版）：ImageInfo.FullImage 懒加载入口。
        /// 用 UriSource + OnLoad 内建解码，替代原自研解码器/CleanupScheduler 链路。
        /// </summary>
        public static BitmapImage? LoadImageOptimized(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            // 全图懒加载在主 UI 线程触发，无并发竞争；查缓存命中直接返回，未命中解码后写入
            var cached = s_fullImageCache.Get(filePath);
            if (cached != null) return cached as BitmapImage;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.RelativeOrAbsolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                s_fullImageCache.Set(filePath, bitmap);
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
                // 控件加载后立即触发一次主动刷新，保证初始可见项都被解码（主动驱动兜底 Loaded 事件）
                RefreshVisibleThumbnails();
            }), DispatcherPriority.Loaded);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _thumbnailListBox = null;
            ImageCollection?.Clear();
        }

        #region 命令实现

        private void ExecuteAddImage()
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
                    AppendFiles(openFileDialog.FileNames);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加图像失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteAddFolder()
        {
            try
            {
                var folderDialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "选择图像文件夹"
                };

                if (folderDialog.ShowDialog() == true)
                {
                    var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" };
                    var imageFiles = Directory.GetFiles(folderDialog.FolderName, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()))
                        .OrderBy(f => f)
                        .ToArray();

                    if (imageFiles.Length == 0)
                    {
                        MessageBox.Show("所选文件夹中没有找到图像文件", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    AppendFiles(imageFiles);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加文件夹失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 追加文件到图像集合（触发后台线程解码缩略图，缓存命中秒出）。
        /// ★ 治本：直接写 ImageInputSource（真相源）而非 ImageCollection（DP 绑定目标）。
        /// ImageInputSource.Images 是只读属性，TwoWay 绑定无法建立，往 ImageCollection 加图不会写回 _images，
        /// 导致 ToConfig() 保存的 FilePaths 为空 → 重启后不恢复（图像源数量空）。
        /// 改用 ActiveInputSource.AddImages() 写 _images，UI 通过 OneWay 绑定自动刷新。
        /// 去重以真相源为准（忽略大小写）：ImagePreviewControl 是共享单例，切换节点时 ActiveInputSource
        /// 重新绑定为另一节点的实例，以实例自身去重天然实现"节点间路径隔离"。
        /// </summary>
        private void AppendFiles(string[] fileNames)
        {
            var source = ActiveInputSource;
            if (source == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"无法添加图像：活动输入源为空", "ImagePreviewControl");
                return;
            }

            var existing = new HashSet<string>(
                source.Images.Select(x => x.FilePath),
                StringComparer.OrdinalIgnoreCase);

            var allImages = fileNames
                .Where(f => !string.IsNullOrEmpty(f))
                .Where(f => existing.Add(f))   // ★ 去重：Add 返回 false 表示已存在，跳过
                .Select(f => new ImageInfo
                {
                    Name = Path.GetFileNameWithoutExtension(f),
                    FilePath = f
                })
                .ToArray();

            // 写真相源 _images（触发 CollectionChanged → OneWay 绑定自动刷新 ImageCollection）
            source.AddImages(allImages);
            if (source.Count > 0)
            {
                CurrentImageIndex = source.Count - allImages.Length;
            }

            // 缩略图由虚拟化容器 Loaded 事件 + 主动刷新（RefreshVisibleThumbnails）双驱动按需解码，
            // 滚动到哪解码到哪，避免海量图片同时排队。
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
                info.EnsureThumbnail();
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
                info.CancelThumbnail();
            }
        }

        private void ExecuteDeleteSingleImage(ImageInfo? imageInfo)
        {
            if (imageInfo == null) return;

            // ★ 走真相源 ActiveInputSource.RemoveImage：改 ImageInputSource._images（与 ImageCollection 同实例，
            // OneWay 绑定自动刷新 UI），并触发 ConfigChanged 实时写回节点 ImageSourceConfig。
            var source = ActiveInputSource;
            if (source == null) return;

            int index = source.Images.IndexOf(imageInfo);
            if (index < 0) return;

            source.RemoveImage(imageInfo);

            // 同步控件选中索引（RemoveImage 内部已修正 CurrentIndex，此处同步 DP）
            if (source.Count == 0)
            {
                CurrentImageIndex = -1;
            }
            else if (CurrentImageIndex >= source.Count)
            {
                CurrentImageIndex = source.Count - 1;
            }
            else if (CurrentImageIndex > index)
            {
                CurrentImageIndex--;
            }

            RefreshImageDisplayState();
        }

        private void ExecuteClearAll()
        {
            if (ImageCollection.Count == 0) return;

            var result = MessageBox.Show(
                $"确定要清除所有 {ImageCollection.Count} 张图像吗？",
                "确认清除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // ★ 走真相源 ActiveInputSource.ClearImages：清 _images 并触发 ConfigChanged 实时写回节点。
                var source = ActiveInputSource;
                if (source != null)
                {
                    source.ClearImages();
                }
                else
                {
                    ImageCollection.Clear();
                }
                CurrentImageIndex = -1;
            }
        }

        private bool CanExecuteClearAll() => ImageCollection?.Count > 0;

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
            control.UpdateImageSelection();
            control.OnPropertyChanged(nameof(ImageCountDisplay));
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
            if (ImageCollection == null) return;

            int count = ImageCollection.Count;
            int currentIndex = CurrentImageIndex;
            for (int i = 0; i < count; i++)
            {
                ImageCollection[i].IsSelected = (i == currentIndex);
            }
        }

        #endregion

        #region 交互事件

        private void OnThumbnailClick(object sender, MouseButtonEventArgs e)
        {
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

            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ImageInfo selectedImage)
            {
                int index = ImageCollection?.IndexOf(selectedImage) ?? -1;
                if (index >= 0)
                {
                    CurrentImageIndex = index;
                }
            }
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
            foreach (var item in FindVisualChildren<ListBoxItem>(_thumbnailListBox))
            {
                if (item.DataContext is ImageInfo info)
                {
                    info.EnsureThumbnail();
                }
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
