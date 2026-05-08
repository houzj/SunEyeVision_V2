using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.UI.ViewModels;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Services.Performance;
using SunEyeVision.UI.Services.Thumbnail.Decoders;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.UI.Views.Controls.Panels
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
    /// 工作流执行请求事件参数
    /// </summary>
    public class WorkflowExecutionRequestEventArgs : EventArgs
    {
        public ImageInfo ImageInfo { get; set; } = null!;
        public int Index { get; set; }
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
                // 首次访问时才加载
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
            // 只有当图像实际已加载时才触发属性变更事件
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
    /// 加载进度信息
    /// </summary>
    public class LoadProgress
    {
        public int CurrentIndex { get; set; }
        public int TotalCount { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public double ProgressPercentage => TotalCount > 0 ? (double)CurrentIndex / TotalCount * 100 : 0;
    }

    /// <summary>
    /// LRU图像缓存
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
        /// 获取或添加缓存
        /// </summary>
        public BitmapSource? GetOrAdd(string filePath, Func<string, BitmapSource?> loader)
        {
            if (_cacheMap.TryGetValue(filePath, out var node))
            {
                // 命中缓存，移到最前
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.Bitmap;
            }

            // 未命中，加载图像
            var bitmap = loader(filePath);
            if (bitmap == null) return null;

            // 添加到缓存
            var entry = new CacheEntry(filePath, bitmap);
            var newNode = _lruList.AddFirst(entry);
            _cacheMap[filePath] = newNode;

            // 超出容量，移除最少使用的
            if (_lruList.Count > _maxCacheSize)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _cacheMap.Remove(lastNode.Value.FilePath);
                    _lruList.RemoveLast();
                }
            }

            return bitmap;
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
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
    /// ImagePreviewControl.xaml 的交互逻辑
    /// </summary>
    public partial class ImagePreviewControl : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        // UI尺寸配置（集中管理，避免硬编码）
        private static class ThumbnailSizes
        {
            // XAML中定义的尺寸（必须与XAML完全一致）
            public const double BorderWidth = 70;          // Border的Width="70"
            public const double BorderHeight = 70;         // Border的Height="70"
            public const double ImageWidth = 60;           // Image的Width="60"
            public const double ImageHeight = 60;          // Image的Height="60"
            public const double PlaceholderWidth = 60;     // 占位符的Width
            public const double PlaceholderHeight = 60;    // 占位符的Height（匹配Image）

            // 布局间距（从XAML Margin="1"计算得出）
            public const double HorizontalMargin = 2;      // 左右各Margin 1

            // 计算属性（供算法使用）
            public static double ItemWidth => BorderWidth + HorizontalMargin;  // 72.0
            public static int ThumbnailLoadSize => (int)ImageWidth;             // 60
        }

        // 图像缓存（LRU）
        private static readonly ImageCache s_fullImageCache = new ImageCache(maxCacheSize: 30);

        // 简化解码器架构：单一解码器（GPU优先，自动降级CPU）
        private static readonly WicGpuDecoder s_decoder = new WicGpuDecoder();

        // 磁盘缓存管理器（60x60高质量缩略图）
        private static readonly ThumbnailCacheManager s_thumbnailCache = new ThumbnailCacheManager();

        // 智能缩略图加载器（组合策略：L1内存 → L2磁盘 → Shell缓存 → EXIF → GPU/CPU解码）
        private static readonly SmartThumbnailLoader s_smartLoader = new SmartThumbnailLoader(s_thumbnailCache, s_decoder);

        // 内存压力监控器（响应系统内存压力）
        private static readonly MemoryPressureMonitor s_memoryMonitor = new MemoryPressureMonitor();

        // ListBox控件引用（用于滚动监听）
        private ListBox? _thumbnailListBox;

        // ★ 防止 CurrentImageIndex DP ↔ ListBox.SelectedItem 循环回调
        private bool _isSyncingListBox;

        // 取消令牌源
        private CancellationTokenSource? _loadingCancellationTokenSource;

        // 滚动防抖定时器
        private DispatcherTimer? _updateRangeTimer;

        // 快速滚动检测
        private double _lastScrollOffset = 0;
        private double _lastScrollSpeed = 0;
        private DateTime _lastScrollSpeedTime = DateTime.MinValue;
        private const double SCROLL_SPEED_THRESHOLD = 500;
        private bool _isFastScrolling = false;
        private const int HIGH_QUALITY_SIZE = 60;

        // 优先级缩略图加载器（替代双系统）
        private readonly PriorityThumbnailLoader _priorityLoader = new PriorityThumbnailLoader();

        public static readonly DependencyProperty AutoSwitchEnabledProperty =
            DependencyProperty.Register("AutoSwitchEnabled", typeof(bool), typeof(ImagePreviewControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty CurrentImageIndexProperty =
            DependencyProperty.Register("CurrentImageIndex", typeof(int), typeof(ImagePreviewControl),
                new PropertyMetadata(-1, OnCurrentImageIndexChanged));

        public static readonly DependencyProperty ImageCollectionProperty =
            DependencyProperty.Register("ImageCollection", typeof(BatchObservableCollection<ImageInfo>), typeof(ImagePreviewControl),
                new PropertyMetadata(null, OnImageCollectionChanged));

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

        public ImagePreviewControl()
        {
            InitializeComponent();
            ImageCollection = new BatchObservableCollection<ImageInfo>();

            // 设置优先级加载器的委托（包含实时可视范围获取）
            _priorityLoader.SetLoadThumbnailFunc(
                (filePath, size, isHighPriority) => LoadThumbnailOptimized(filePath, size, isHighPriority), 
                (filePath, thumbnail) => s_thumbnailCache?.AddToMemoryCache(filePath, thumbnail),
                () => GetVisibleRange());  // ◆实时获取可视范围

            // ===== 内存压力监控初始化 =====
            s_memoryMonitor.MemoryPressureChanged += OnMemoryPressureChanged;
            s_memoryMonitor.Start();
// 订阅 Unloaded 事件以清理资源
            Unloaded += (s, e) =>
            {
                s_memoryMonitor.MemoryPressureChanged -= OnMemoryPressureChanged;
            };

            // 输出加载器状态
            AddImageCommand = new RelayCommand(ExecuteAddImage);
            AddFolderCommand = new RelayCommand(ExecuteAddFolder);
            DeleteSingleImageCommand = new RelayCommand<ImageInfo>(ExecuteDeleteSingleImage);
            ClearAllCommand = new RelayCommand(ExecuteClearAll, CanExecuteClearAll);

            ImageCollection.CollectionChanged += (s, e) =>
            {
                // UpdateDisplayIndices(); // 不再需要显示序号，移除此调用
                OnPropertyChanged(nameof(ImageCountDisplay));
            };

            Loaded += OnControlLoaded;
            Unloaded += OnUnloaded;

            // 初始化ListBox引用（需要在Loaded之后）
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                _thumbnailListBox = FindName("ThumbnailListBox") as ListBox;
            }));
        }

        /// <summary>
        /// 控件加载完成
        /// </summary>
        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            // ===== P2优化: GPU缓存预热 =====
            PreloadGPUCache();
            
            // 延迟初始化，确保ListBox已完全加载
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                _thumbnailListBox = FindName("ThumbnailListBox") as ListBox;
                if (_thumbnailListBox != null)
                {
                    // ★ScrollChanged 已改为 XAML 附加事件绑定 (ScrollViewer.ScrollChanged)
                    //   不再依赖 FindVisualChild 进行订阅
                    
                    // ===== 容器回收事件监听已禁用 =====
                    // 原因：快速来回滚动时会频繁触发容器回收，导致性能下降
                    // 
                    // 状态不一致问题已通过以下方式解决：
                    // 1. MemoryPressureMonitor.SyncLoadedIndicesWithActualThumbnails()
                    //    在内存压力大时自动同步状态
                    // 2. 保留 Thumbnail 可以让滚回时即时显示
                    //
                    // 优点：
                    // - 避免频繁加载/卸载循环
                    // - 滚动流畅，内存由压力机制自动管理
                    // - 已加载的缩略图保留在内存中，滚回时即时显示
                    // =====
                    // VirtualizingStackPanel.AddCleanUpVirtualizedItemHandler(
                    //     _thumbnailListBox, 
                    //     OnCleanUpVirtualizedItem);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        
        // ===== 容器回收事件处理已禁用（原因见上） ====

        /// <summary>
        /// P2优化: 解码器初始化（减少首次解码延迟）
        /// ◆方案二：同时初始化GPU和CPU解码器
        /// </summary>
        private void PreloadGPUCache()
        {
            Task.Run(() =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    // 初始化解码器
                    s_decoder.Initialize();
                    sw.Stop();
                }
                catch (Exception ex)
                {
                    VisionLogger.Instance.Warning($"[ImagePreview] 解码器初始化失败: {ex.Message}", "ImagePreviewControl");
                }
            });
        }

        /// <summary>
        /// 查找视觉子元素
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var resultOfChild = FindVisualChild<T>(child);
                if (resultOfChild != null)
                    return resultOfChild;
            }
            return null;
        }

        /// <summary>
        /// 控件卸载时清理资源
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // 取消正在进行的加载操作
            _loadingCancellationTokenSource?.Cancel();
            _loadingCancellationTokenSource?.Dispose();
            _loadingCancellationTokenSource = null;

            // 清理优先级加载器
            _priorityLoader.Dispose();

            // 清理所有图像资源
            if (ImageCollection != null)
            {
                foreach (var imageInfo in ImageCollection)
                {
                    imageInfo.Thumbnail = null;
                    imageInfo.ReleaseFullImage();
                }
                ImageCollection.Clear();
            }
        }

        /// <summary>
        /// 优化的全分辨率图像加载（静态方法，可从ImageInfo调用）
        /// ◆优化：使用 CleanupScheduler 保卫 + StreamSource 立即加载
        /// </summary>
        public static BitmapImage? LoadImageOptimized(string filePath)
        {
            // ◆修复：加载前检查文件是否存在，避免抛出 FileNotFoundException
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }
            
            // ◆核心修复：使用 CleanupScheduler 保卫文件访问
            CleanupScheduler.MarkFileInUse(filePath);
            
            try
            {
                return s_fullImageCache.GetOrAdd(filePath, fp =>
                {
                    // 再次检查（缓存内部可能访问不同的路径）
                    if (string.IsNullOrEmpty(fp) || !File.Exists(fp))
                    {
                        return null;
                    }
                    
                    try
                    {
                        // ◆优化：先读取文件到内存，避免 UriSource 延迟加载问题
                        byte[] imageBytes;
                        using (var fs = new FileStream(fp, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan))
                        {
                            imageBytes = new byte[fs.Length];
                            int bytesRead = fs.Read(imageBytes, 0, imageBytes.Length);
                            if (bytesRead != imageBytes.Length && imageBytes.Length > 0)
                            {
                                Array.Resize(ref imageBytes, bytesRead);
                            }
                        }
                        
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                        bitmap.StreamSource = new MemoryStream(imageBytes);
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                    catch (Exception ex)
                    {
                        VisionLogger.Instance.Warning($"[ImagePreview] 全分辨率图像解码失败: {fp}, {ex.Message}", "ImagePreviewControl");
                        return null;
                    }
                }) as BitmapImage;
            }
            finally
            {
                // ◆确保释放文件引用
                CleanupScheduler.ReleaseFile(filePath);
            }
        }

        /// <summary>
        /// 优化的缩略图加载（智能加载器 - 3层架构）
        /// 加载策略优先级：L1内存 → L2磁盘(Shell优先) → GPU解码
        /// </summary>
        private static BitmapImage? LoadThumbnailOptimized(string filePath, int size = -1, bool isHighPriority = false)
        {
            // 如果size为-1，使用配置的缩略图尺寸
            if (size < 0)
            {
                size = ThumbnailSizes.ThumbnailLoadSize;
            }

            // 使用智能加载器（自动选择最快方式）
            var thumbnail = s_smartLoader.LoadThumbnail(filePath, size, isHighPriority);

            return thumbnail;
        }

        /// <summary>
        /// 异步加载缩略图
        /// </summary>
        private static Task<BitmapImage?> LoadThumbnailAsync(string filePath, int size = 80)
        {
            return Task.Run(() => LoadThumbnailOptimized(filePath, size));
        }

        /// <summary>
        /// 滚动变化事件处理（带防抖机制 + 动态质量切换）
        /// </summary>
        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // ★诊断日志：确认事件已触发
#if DEBUG
            VisionLogger.Instance.Info(
                $"[ScrollDiag] OnScrollChanged ENTERED: " +
                $"HOffset={e.HorizontalOffset} HChange={e.HorizontalChange} " +
                $"ExtentW={e.ExtentWidth} ViewportW={e.ViewportWidth}",
                "ImagePreviewControl");
#endif

            var now = DateTime.Now;

            // ===== 计算滚动速度 =====
            if (_lastScrollSpeedTime != DateTime.MinValue)
            {
                var timeSinceLast = now - _lastScrollSpeedTime;
                if (timeSinceLast.TotalSeconds > 0)
                {
                    var scrollViewer = FindVisualChild<ScrollViewer>(_thumbnailListBox);
                    if (scrollViewer != null)
                    {
                        var currentOffset = scrollViewer.HorizontalOffset;
                        var offsetDelta = Math.Abs(currentOffset - _lastScrollOffset);
                        var scrollSpeed = offsetDelta / timeSinceLast.TotalSeconds;
                        _lastScrollSpeed = scrollSpeed;
                        _lastScrollOffset = currentOffset;

                        bool wasFastScrolling = _isFastScrolling;
                        _isFastScrolling = scrollSpeed > SCROLL_SPEED_THRESHOLD;

                        // 快速滚动开始：使用低质量模式
                        if (_isFastScrolling && !wasFastScrolling)
                        {
                            _priorityLoader.UseLowQuality = true;
                        }

                        // 滚动停止：恢复高质量
                        if (!_isFastScrolling && wasFastScrolling)
                        {
                            _priorityLoader.UseLowQuality = false;

                            // ◆关键修复：先检查集合是否有效
                            var collection = ImageCollection;
                            if (collection != null && collection.Count > 0)
                            {
                                // 立即触发加载新进入可视区域的缩略图
                                UpdateLoadRange();

                                // 延迟加载高质量缩略图
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    var innerCollection = ImageCollection;
                                    if (innerCollection != null && innerCollection.Count > 0)
                                    {
                                        UpgradeToHighQuality();
                                    }
                                }), DispatcherPriority.Background);
                            }
                        }
                    }
                }
            }
            _lastScrollSpeedTime = now;

            // 立即入队可视区域图片（不等防抖）
            var visibleCollection = ImageCollection;
            if (visibleCollection == null || visibleCollection.Count == 0)
            {
                return;
            }

            var (firstVis, lastVis) = GetVisibleRange();
            if (firstVis >= 0 && lastVis >= 0)
            {
                VisionLogger.Instance.Info(
                    $"[ScrollDiag] scroll offset={_lastScrollOffset:F1} speed={_lastScrollSpeed:F1} " +
                    $"range=[{firstVis}-{lastVis}] total={visibleCollection.Count} " +
                    $"async={_isFastScrolling}",
                    "ImagePreviewControl");
                _priorityLoader.UpdateVisibleRangeImmediate(firstVis, lastVis, visibleCollection.Count);
            }
            else
            {
                VisionLogger.Instance.Warning($"[ScrollDiag] GetVisibleRange返回无效值 firstVis={firstVis} lastVis={lastVis}", "ImagePreviewControl");
            }

            // 防抖：滚动停止后触发清理和范围调整
            var debounceTime = _isFastScrolling ? 400 : 200;

            if (_updateRangeTimer == null)
            {
                _updateRangeTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(debounceTime)
                };
                _updateRangeTimer.Tick += (s, args) =>
                {
                    _updateRangeTimer?.Stop();
                    var collection = ImageCollection;
                    if (collection == null || collection.Count == 0)
                    {
                        return;
                    }

                    var (first, last) = GetVisibleRange();
                    if (first >= 0)
                    {
                        _priorityLoader.UpdateVisibleRange(first, last, collection.Count);
                    }
                };
            }
            else
            {
                _updateRangeTimer.Interval = TimeSpan.FromMilliseconds(debounceTime);
            }
            _updateRangeTimer.Stop();
            _updateRangeTimer.Start();
        }

        /// <summary>
        /// P0优化: 将低质量缩略图升级为高质量（同时处理无缩略图的情况）
        /// 注意：必须在UI线程调用此方法，内部会切换到后台线程处理
        /// ◆虚拟化安全：使用 ItemContainerGenerator 精确获取可视范围
        /// </summary>
        private void UpgradeToHighQuality()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            // ===== 关键修复：在UI线程获取所有需要的数据 =====
            if (ImageCollection == null || _thumbnailListBox == null)
            {
return;
            }

            // ◆使用虚拟化安全的方法获取可视范围
            var (firstVisible, lastVisible) = GetVisibleRange();
            
            if (firstVisible == -1 || lastVisible == -1)
            {
return;
            }

            var totalCount = ImageCollection.Count;

            // 在UI线程提取需要处理的项
            var itemsToProcess = new List<(int Index, string FilePath, bool HasThumbnail, double ThumbnailWidth)>();
            for (int i = firstVisible; i <= lastVisible; i++)
            {
                if (i >= 0 && i < ImageCollection.Count)
                {
                    var imageInfo = ImageCollection[i];
                    itemsToProcess.Add((i, imageInfo.FilePath, imageInfo.Thumbnail != null, 
                        imageInfo.Thumbnail?.Width ?? 0));
                }
            }

            // 异步加载高质量缩略图
            Task.Run(() =>
            {
                int loadedCount = 0;
                int upgradedCount = 0;
                int errorCount = 0;
                
                try
                {
                    foreach (var item in itemsToProcess)
                    {
                        // 情况1：无缩略图 - 直接加载高质量
                        if (!item.HasThumbnail)
                        {
                            try
                            {
                                var highQuality = LoadThumbnailOptimized(item.FilePath, HIGH_QUALITY_SIZE);
                                if (highQuality != null)
                                {
                                    // 使用BeginInvoke更新UI，并传递索引
                                    int index = item.Index;
                                    Application.Current?.Dispatcher.BeginInvoke(() =>
                                    {
                                        if (index >= 0 && index < ImageCollection.Count)
                                        {
                                            ImageCollection[index].Thumbnail = highQuality;
                                        }
                                    }, DispatcherPriority.Background);
                                    loadedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
}
                        }
                        // 情况2：低质量缩略图 - 升级为高质量
                        else if (item.ThumbnailWidth < HIGH_QUALITY_SIZE)
                        {
                            try
                            {
                                var highQuality = LoadThumbnailOptimized(item.FilePath, HIGH_QUALITY_SIZE);
                                if (highQuality != null)
                                {
                                    // 使用BeginInvoke更新UI，并传递索引
                                    int index = item.Index;
                                    Application.Current?.Dispatcher.BeginInvoke(() =>
                                    {
                                        if (index >= 0 && index < ImageCollection.Count)
                                        {
                                            ImageCollection[index].Thumbnail = highQuality;
                                        }
                                    }, DispatcherPriority.Background);
                                    upgradedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                VisionLogger.Instance.Warning($"[ImagePreview] 缩略图加载失败({item.FilePath}): {ex.Message}", "ImagePreviewControl");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    VisionLogger.Instance.Warning($"[ImagePreview] 缩略图升级异常: {ex.Message}", "ImagePreviewControl");
                }
                
                sw.Stop();
            });
        }

        #region 命令实现

        /// <summary>
        /// 添加图像
        /// </summary>
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
                    var fileNames = openFileDialog.FileNames;

                    // 创建新的取消令牌
                    _loadingCancellationTokenSource?.Cancel();
                    _loadingCancellationTokenSource?.Dispose();
                    _loadingCancellationTokenSource = new CancellationTokenSource();

                    var cancellationToken = _loadingCancellationTokenSource.Token;

                    await LoadImagesOptimizedAsync(fileNames, cancellationToken, append: true);
                }
            }
            catch (OperationCanceledException)
            {
                // 用户取消了操作
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加图像失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 计算最优并发数（动态并发数优化）
        /// </summary>
        private int CalculateOptimalConcurrency()
        {
            int cpuCount = Environment.ProcessorCount;
            bool isInitialized = s_decoder.IsInitialized;

            if (isInitialized)
            {
                // 解码器已初始化：适中并发数，充分利用并行能力
                int concurrency = Math.Min(8, Math.Max(4, (int)(cpuCount / 1.5)));
return concurrency;
            }
            else
            {
                // CPU模式：充分利用多核
                int cpuConcurrency = Math.Max(2, (int)(cpuCount * 0.75));
return cpuConcurrency;
            }
        }

        /// <summary>
        /// 优化的图像加载方法（简化版：使用优先级加载器）
        /// 优化：统一计时日志、文件预取、等待可视区域完成
        /// </summary>
        /// <param name="append">true=追加到已有图像，false=替换全部</param>
        public async Task LoadImagesOptimizedAsync(
            string[] fileNames,
            CancellationToken cancellationToken,
            bool append = false)
        {
            // ===== 统一计时：从用户角度记录有意义的时间 =====
            var totalSw = Stopwatch.StartNew();
            var firstImageSw = Stopwatch.StartNew();
            TimeSpan? firstImageTime = null;
            TimeSpan? visibleAreaTime = null;

            try
            {
                if (!append)
                {
                    // 替换模式：清空优先级加载器的状态
                    _priorityLoader.ClearState();
                }

                // ◆日志优化：重置首张图片追踪计数器
                SmartThumbnailLoader.ResetLoadCounter();
// ===== 步骤1：预创建所有ImageInfo对象 =====
                var step1Sw = Stopwatch.StartNew();
                var allImages = new ImageInfo[fileNames.Length];
                for (int i = 0; i < fileNames.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    allImages[i] = new ImageInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(fileNames[i]),
                        FilePath = fileNames[i],
                        Thumbnail = null,
                        FullImage = null
                    };
                }
                step1Sw.Stop();
// ===== 步骤2：更新UI（批量添加，性能优化） =====
                var step2Sw = Stopwatch.StartNew();
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (append)
                    {
                        // 追加模式：保留已有图像，新增的添加到末尾
                        ImageCollection.AddRange(allImages);

                        if (ImageCollection.Count > 0 && allImages.Length > 0)
                        {
                            // 跳转到第一张新添加的图像
                            CurrentImageIndex = ImageCollection.Count - allImages.Length;
                        }
                    }
                    else
                    {
                        // 替换模式：使用批量替换，只触发一次Reset事件，性能提升30-50倍
                        ImageCollection.ReplaceRange(allImages);

                        if (ImageCollection.Count > 0)
                        {
                            CurrentImageIndex = 0;
                        }
                    }

                    // 设置图像集合引用
                    _priorityLoader.SetImageCollection(ImageCollection);

                    // 注意：移除UpdateLayout()调用，依赖虚拟化自动处理
                }, System.Windows.Threading.DispatcherPriority.Normal);
                step2Sw.Stop();
                // ===== 步骤3：使用优先级加载器加载首页 =====
                var step3Sw = Stopwatch.StartNew();

                // 设置首张图片加载完成的回调
                var firstImageTcs = new TaskCompletionSource<bool>();

                await _priorityLoader.LoadInitialScreenAsync(fileNames, ImageCollection, index =>
                {
                    firstImageSw.Stop();
                    firstImageTime = firstImageSw.Elapsed;
                    firstImageTcs.TrySetResult(true);
                });

                // 等待首张图片加载完成
                await firstImageTcs.Task;
                step3Sw.Stop();

                // ===== 步骤3b：等待可视区域所有缩略图加载完成 =====
                var allVisibleSw = Stopwatch.StartNew();
                var allVisibleTcs = new TaskCompletionSource<TimeSpan>();
                Action<int, TimeSpan> onVisibleComplete = null!;
                onVisibleComplete = (_, dur) =>
                {
                    _priorityLoader.VisibleAreaLoadingCompleted -= onVisibleComplete;
                    allVisibleTcs.TrySetResult(dur);
                };
                _priorityLoader.VisibleAreaLoadingCompleted += onVisibleComplete;

                // 等待可视区域加载（最长等待30秒）
                var visibleTask = allVisibleTcs.Task;
                var timeoutTask = Task.Delay(30000);
                bool visibleCompleted = await Task.WhenAny(visibleTask, timeoutTask) == visibleTask;
                if (!visibleCompleted)
                {
                    _priorityLoader.VisibleAreaLoadingCompleted -= onVisibleComplete;
                }
                allVisibleSw.Stop();
                TimeSpan? visibleAreaDuration = visibleCompleted ? await visibleTask : null;

                // 兜底：如果超时仍有缩略图为空，强制触发升级加载
                if (!visibleCompleted)
                {
                    VisionLogger.Instance.Warning("[ImagePreview] 可视区域缩略图加载超时，触发兜底加载", "ImagePreviewControl");
                    await Dispatcher.InvokeAsync(() => UpgradeToHighQuality());
                }

                // ===== 步骤4：文件预取（在首张显示后启动，避免I/O竞争） =====
                var prefetchSw = Stopwatch.StartNew();
                int prefetchCount = Math.Min(20, fileNames.Length);
                _ = Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < prefetchCount; i++)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            s_smartLoader.PrefetchFile(fileNames[i]);
                        }
                    }
                    catch (Exception ex)
                    {
                        VisionLogger.Instance.Warning($"[ImagePreview] 文件预取异常: {ex.Message}", "ImagePreviewControl");
                    }
                }, cancellationToken);
                prefetchSw.Stop();

                // ===== 步骤5：后台预加载全分辨率图像 =====
                _ = Task.Run(() =>
                {
                    int preloadCount = Math.Min(3, fileNames.Length);
                    for (int i = 0; i < preloadCount; i++)
                    {
                        try
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            var fullImage = LoadImageOptimized(fileNames[i]);
                            if (fullImage != null && !cancellationToken.IsCancellationRequested)
                            {
                                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    try
                                    {
                                        if (i >= 0 && i < ImageCollection.Count)
                                        {
                                            var imageInfo = ImageCollection[i];
                                            if (imageInfo != null)
                                            {
                                                imageInfo.SetFullImage(fullImage);
                                            }
                                        }
                                    }
                                    catch (InvalidOperationException)
                                    {
                                    }
                                    catch (Exception ex)
                                    {
                                        VisionLogger.Instance.Warning($"[ImagePreview] 后台预加载全分辨率失败: {fileNames[i]}, {ex.Message}", "ImagePreviewControl");
                                    }
                                }), System.Windows.Threading.DispatcherPriority.Background);
                            }
                        }
                        catch (Exception ex)
                        {
                            VisionLogger.Instance.Warning($"[ImagePreview] 全分辨率预加载失败: {fileNames[i]}, {ex.Message}", "ImagePreviewControl");
                        }
                    }
                }, cancellationToken);

                totalSw.Stop();

                // ===== 图像加载性能报告（含完整解码统计） =====
                var imageCount = allImages.Length;
                var appendMode = append ? "追加" : "新建";
                var totalMs = totalSw.ElapsedMilliseconds;
                var stats = s_smartLoader.GetStatistics();

                var stepTimings = new List<(string Step, long Ms)>
                {
                    ("预创建ImageInfo对象", step1Sw.ElapsedMilliseconds),
                    ("UI批量添加", step2Sw.ElapsedMilliseconds),
                    ("首张缩略图解码", (long)(firstImageTime?.TotalMilliseconds ?? 0)),
                    ("可视区域加载完成", allVisibleSw.ElapsedMilliseconds),
                    ("文件预读", prefetchSw.ElapsedMilliseconds)
                };

                var maxStep = stepTimings.MaxBy(t => t.Ms);
                var reportLines = new List<string>
                {
                    $"===== 图像加载性能报告 =====",
                    $"模式: {appendMode}, 图像数: {imageCount}, 总耗时: {totalMs}ms",
                    $"首张缩略图: {firstImageTime?.TotalMilliseconds:F0}ms | 可视区域完成: {visibleAreaDuration?.TotalMilliseconds:F0}ms{(visibleCompleted ? "" : " ⚠超时")}",
                    $"步骤  名称              耗时(ms)    占比  说明",
                    $"────  ──────────────── ────────  ──────  ──────────────────"
                };
                foreach (var (step, ms) in stepTimings)
                {
                    var pct = totalMs > 0 ? (double)ms / totalMs * 100 : 0;
                    var marker = step == maxStep.Step ? " ◄瓶颈" : "";
                    reportLines.Add($"  {stepTimings.IndexOf((step, ms)) + 1,2}.  {step,-18} {ms,8}  {pct,5:F1}%{marker}");
                }
                reportLines.Add($"──────────────────────────────────");
                reportLines.Add($"  缓存统计: {stats}");
                reportLines.Add($"  瓶颈: [{maxStep.Step}]={maxStep.Ms}ms");
                VisionLogger.Instance.Info(string.Join("\n", reportLines), "ImagePreviewControl");
            }
            catch (OperationCanceledException)
            {
                // 操作取消，无需日志
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Warning($"[ImagePreview] 图像加载异常: {ex.Message}", "ImagePreviewControl");
            }
        }



        /// <summary>
        /// 添加文件夹
        /// </summary>
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
                    var folderPath = folderDialog.FolderName;

                    var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" };

                    var imageFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()))
                        .OrderBy(f => f)
                        .ToArray();

                    if (imageFiles.Length == 0)
                    {
                        MessageBox.Show("所选文件夹中没有找到图像文件", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 创建新的取消令牌
                    _loadingCancellationTokenSource?.Cancel();
                    _loadingCancellationTokenSource?.Dispose();
                    _loadingCancellationTokenSource = new CancellationTokenSource();

                    var cancellationToken = _loadingCancellationTokenSource.Token;

                    await LoadImagesOptimizedAsync(imageFiles, cancellationToken, append: true);
                }
            }
            catch (OperationCanceledException)
            {
                // 用户取消了操作
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加文件夹失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除单个图像（通过缩略图上的删除按钮）
        /// </summary>
        private void ExecuteDeleteSingleImage(ImageInfo? imageInfo)
        {
if (imageInfo == null)
            {
return;
            }

            int index = ImageCollection.IndexOf(imageInfo);
            if (index >= 0)
            {
                ImageCollection.RemoveAt(index);
// 关键修复：删除图片后，所有后续图片的索引都发生了错位
                // 必须清除PriorityLoader的_loadedIndices缓存，否则会导致"假已加载"状态
_priorityLoader.ClearState();
// 调整当前索引
                int oldIndex = CurrentImageIndex;
                if (ImageCollection.Count == 0)
                {
                    CurrentImageIndex = -1;
                }
                else if (CurrentImageIndex >= ImageCollection.Count)
                {
                    CurrentImageIndex = ImageCollection.Count - 1;
                }
                else if (CurrentImageIndex > index)
                {
                    // 如果删除的是当前索引之前的图像，当前索引需要减1
                    CurrentImageIndex--;
                }
// 无论索引是否变化，都强制刷新相关UI状态
                RefreshImageDisplayState();
            }
        }

        /// <summary>
        /// 清除所有图像
        /// </summary>
        private void ExecuteClearAll()
        {
            if (ImageCollection.Count == 0)
                return;

            var result = MessageBox.Show(
                $"确定要清除所有 {ImageCollection.Count} 张图像吗？",
                "确认清除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 清除加载器状态
                _priorityLoader.ClearState();
                _priorityLoader.SetImageCollection(null);

                // 清除所有缓存层级
                s_thumbnailCache?.ClearCache();        // 磁盘缓存 L2 + 内存缓存 L1
                s_fullImageCache.Clear();              // 全尺寸图像 LRU 缓存
                s_smartLoader.ClearPrefetchCache();    // 预取缓存

                ImageCollection.Clear();
                CurrentImageIndex = -1;
            }
        }

        private bool CanExecuteClearAll()
        {
            return ImageCollection?.Count > 0;
        }



        #endregion

        #region 辅助方法

        /// <summary>
        /// 当前图像索引更改回调
        /// ★ 防闪烁：使用 _isSyncingListBox 标志位防止 DP ↔ ListBox 循环回调
        /// </summary>
        private static void OnCurrentImageIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
        {
            var control = (ImagePreviewControl)d;
            control.UpdateImageSelection();
            control.OnPropertyChanged(nameof(ImageCountDisplay));

            // 同步 ListBox 的 SelectedItem，使用标志位防止循环
            if (control._thumbnailListBox != null && control.ImageCollection != null &&
                control.CurrentImageIndex >= 0 && control.CurrentImageIndex < control.ImageCollection.Count)
            {
                control._isSyncingListBox = true;
                var selectedImage = control.ImageCollection[control.CurrentImageIndex];
                control._thumbnailListBox.SelectedItem = selectedImage;
                control._isSyncingListBox = false;
            }
        }

        /// <summary>
        /// 图像集合更改回调
        /// 优化：延迟渲染 + 占位符策略，避免切换节点时卡顿
        /// </summary>
        private static void OnImageCollectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ImagePreviewControl)d;
            var oldCollection = e.OldValue as BatchObservableCollection<ImageInfo>;
            var newCollection = e.NewValue as BatchObservableCollection<ImageInfo>;

            // ◆优化：如果新旧集合引用相同，跳过处理
            // 这避免了从非采集节点切回采集节点时不必要的缩略图清空
            if (oldCollection == newCollection && newCollection != null)
            {
return;
            }

            // 更新基本属性
            control.UpdateImageSelection();
            control.OnPropertyChanged(nameof(ImageCountDisplay));

            // ◆关键修复：当集合变为空或 null 时，必须清理加载器状态
            // 否则 _priorityLoader 仍持有旧集合引用，导致异步任务操作旧数据产生 NullReferenceException
            if (newCollection == null || newCollection.Count == 0)
            {
                control._priorityLoader.ClearState();
                control._priorityLoader.SetImageCollection(null);
return;
            }

            // 延迟渲染优化：异步加载可视区域缩略图
            control.ScheduleDeferredThumbnailLoading(newCollection);
        }

        /// <summary>
        /// 延迟缩略图加载计时器
        /// </summary>
        private DispatcherTimer? _deferredLoadingTimer;

        /// <summary>
        /// 安排延迟缩略图加载（避免切换节点时卡顿）
        /// </summary>
        private void ScheduleDeferredThumbnailLoading(BatchObservableCollection<ImageInfo> collection)
        {
// 取消之前的延迟加载任务
            _deferredLoadingTimer?.Stop();
            _priorityLoader.ClearState();

            // ◆关键修复：无论是否有缩略图，都必须设置新的图像集合引用
            // 否则 _priorityLoader 的 _imageCollection 仍指向旧节点，导致索引越界
            _priorityLoader.SetImageCollection(collection);

            // 如果集合中没有已加载的缩略图，需要主动触发加载
            bool hasAnyThumbnail = collection.Any(img => img.Thumbnail != null);
// ◆修复：无论是否有缩略图，都需要触发可视区域加载
            // 原问题：当没有缩略图时直接返回，依赖滚动事件触发，但切换节点后可能没有滚动事件
            if (!hasAnyThumbnail)
            {
                // 没有已加载的缩略图，延迟后触发可视区域加载
                // 使用更长的延迟确保UI布局完成
                _deferredLoadingTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100) // 100ms 延迟，确保布局完成
                };
                _deferredLoadingTimer.Tick += (s, args) =>
                {
                    _deferredLoadingTimer?.Stop();
                    // ◆关键修复：重新验证集合是否仍然有效
                    var currentCollection = ImageCollection;
                    if (currentCollection == null || currentCollection.Count == 0)
                    {
return;
                    }
LoadVisibleRangeThumbnails(currentCollection);
                };
                _deferredLoadingTimer.Start();
                return;
            }

            // 情况2：集合中已有缩略图（从其他节点切换过来）
            // 策略：先清空显示占位符，再异步加载可视区域
            var sw = Stopwatch.StartNew();

            // 清空所有缩略图显示（保留文件路径数据）
            foreach (var image in collection)
            {
                image.Thumbnail = null;
            }
            sw.Stop();
// 延迟一帧后开始加载可视区域（确保占位符已渲染）
            _deferredLoadingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 50ms 延迟，确保 UI 更新完成
            };
            _deferredLoadingTimer.Tick += (s, args) =>
            {
                _deferredLoadingTimer?.Stop();
                // ◆关键修复：重新验证集合是否仍然有效
                var currentCollection = ImageCollection;
                if (currentCollection == null || currentCollection.Count == 0)
                {
return;
                }
LoadVisibleRangeThumbnails(currentCollection);
            };
            _deferredLoadingTimer.Start();
        }

        /// <summary>
        /// 异步加载可视区域的缩略图
        /// ◆增强重试机制：当可视范围获取失败时自动重试
        /// </summary>
        private void LoadVisibleRangeThumbnails(BatchObservableCollection<ImageInfo> collection)
        {
            if (collection == null || collection.Count == 0)
                return;

            var sw = Stopwatch.StartNew();
// 获取可视范围
            var (firstVisible, lastVisible) = GetVisibleRange();

            // ◆增强重试：如果可视范围无效，延迟重试
            if (firstVisible == -1 || lastVisible == -1)
            {
// 延迟重试（最多重试2次）
                _deferredLoadingTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                int retryCount = 0;
                _deferredLoadingTimer.Tick += (s, args) =>
                {
                    retryCount++;
                    var (retryFirst, retryLast) = GetVisibleRange();
                    
                    if (retryFirst >= 0 && retryLast >= 0)
                    {
                        _deferredLoadingTimer?.Stop();
ExecuteLoadVisibleRange(collection, retryFirst, retryLast, sw);
                    }
                    else if (retryCount >= 2)
                    {
                        // 重试失败，使用默认值强制加载
                        _deferredLoadingTimer?.Stop();
                        int defaultLast = Math.Min(collection.Count - 1, 15);
ExecuteLoadVisibleRange(collection, 0, defaultLast, sw);
                    }
                };
                _deferredLoadingTimer.Start();
                return;
            }
ExecuteLoadVisibleRange(collection, firstVisible, lastVisible, sw);
        }

        /// <summary>
        /// 执行可视区域加载
        /// </summary>
        private void ExecuteLoadVisibleRange(BatchObservableCollection<ImageInfo> collection, int firstVisible, int lastVisible, Stopwatch sw)
        {
            // 设置当前图像索引
            if (CurrentImageIndex < 0 && collection.Count > 0)
            {
                CurrentImageIndex = 0;
            }

            // 使用优先级加载器加载可视区域
            var filePaths = collection.Select(img => img.FilePath).ToArray();
            _ = _priorityLoader.LoadInitialScreenAsync(filePaths, collection, index =>
            {
                // 首张图片加载完成
                sw.Stop();
});
        }

        /// <summary>
        /// 图像运行模式更改回调
        /// </summary>
        private static void OnImageRunModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
        {
            var control = (ImagePreviewControl)d;
            control.OnPropertyChanged(nameof(ImageRunMode));
            
            // 刷新命令状态
            CommandManager.InvalidateRequerySuggested();
        }





        /// <summary>
        /// 刷新图像显示状态（修复删除图片后UI不更新和缩略图显示问题）
        /// </summary>
        private void RefreshImageDisplayState()
        {
            // 强制刷新ImageCountDisplay（确保删除后计数正确更新）
            OnPropertyChanged(nameof(ImageCountDisplay));

            // 强制刷新缩略图选中状态
            UpdateImageSelection();

            // 确保触发可视区域更新
            if (CurrentImageIndex >= 0)
            {
                // 同步ListBox的SelectedItem（带防循环标志）
                if (_thumbnailListBox != null && ImageCollection != null &&
                    CurrentImageIndex >= 0 && CurrentImageIndex < ImageCollection.Count)
                {
                    _isSyncingListBox = true;
                    var selectedImage = ImageCollection[CurrentImageIndex];
                    _thumbnailListBox.SelectedItem = selectedImage;
                    _isSyncingListBox = false;
                }

                // 触发可视区域更新
                UpdateLoadRange();
            }
            else
            {
                // 如果没有选中图像，清除ListBox选中项
                if (_thumbnailListBox != null)
                {
                    _thumbnailListBox.SelectedItem = null;
                }
            }
        }

        /// <summary>
        /// 更新图像选中状态（增量更新，仅变更新旧两项）
        /// ★ 防闪烁：避免全量遍历触发不必要的 DataTrigger 刷新
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

        /// <summary>
        /// 缩略图点击事件处理
        /// </summary>
        private void OnThumbnailClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
// 如果点击的是删除按钮或CheckBox，跳过处理
            DependencyObject? current = e.OriginalSource as DependencyObject;
            while (current != null)
            {
                if (current is Button || current is CheckBox)
                {
return;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            if (sender is Border border && border.Tag is ImageInfo imageInfo)
            {
                int index = ImageCollection.IndexOf(imageInfo);
if (index >= 0)
                {
                    if (ImageRunMode == ImageRunMode.运行选择)
                    {
                        // ◆运行选择模式：只有已勾选的图片才执行工作流
                        if (imageInfo.IsForRun)
                        {
RequestWorkflowExecution(imageInfo, index);
                        }
                        else
                        {
}
                    }
                    else
                    {
                        // ◆运行全部模式：点击任意图片执行工作流
RequestWorkflowExecution(imageInfo, index);
                    }
                }
            }
        }


        /// <summary>
        /// 请求执行工作流
        /// </summary>
        private void RequestWorkflowExecution(ImageInfo imageInfo, int index)
        {
            // 切换当前显示的图像
            CurrentImageIndex = index;
            
            // 调试：检查事件订阅者数量
            var subscriberCount = WorkflowExecutionRequested?.GetInvocationList()?.Length ?? 0;
// 触发工作流执行请求事件
            WorkflowExecutionRequested?.Invoke(this, new WorkflowExecutionRequestEventArgs
            {
                ImageInfo = imageInfo,
                Index = index
            });
}

        /// <summary>
        /// 缩略图ListBox选择改变事件处理
        /// ★ 防闪烁：跳过由代码同步触发的回调，防止 DP ↔ ListBox 循环
        /// </summary>
        private void OnThumbnailListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 由 OnCurrentImageIndexChanged 同步 SelectedItem 触发，跳过
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

        /// <summary>
        /// 运行模式按钮点击事件处理
        /// </summary>
        private void OnRunModeButtonClick(object sender, RoutedEventArgs e)
        {
            if (RunModePopup != null)
            {
                RunModePopup.IsOpen = !RunModePopup.IsOpen;
            }
        }

        /// <summary>
        /// 运行模式下拉列表鼠标按下事件处理
        /// 使用PreviewMouseDown确保在点击任意项时都能立即响应
        /// </summary>
        private void OnRunModeListBoxPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (RunModeListBox == null || RunModePopup == null)
                return;

            // 获取点击的源元素
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null)
                return;

            // 查找对应的ListBoxItem
            var listBoxItem = FindParent<ListBoxItem>(originalSource);
            if (listBoxItem == null)
                return;

            // 获取选中项
            ImageRunMode? selectedMode = null;
            if (listBoxItem.Content is TextBlock textBlock && textBlock.DataContext is ImageRunMode mode1)
            {
                selectedMode = mode1;
            }
            else if (listBoxItem.DataContext is ImageRunMode mode2)
            {
                selectedMode = mode2;
            }

            // 更新运行模式
            if (selectedMode.HasValue)
            {
                ImageRunMode = selectedMode.Value;
            }

            // 立即关闭Popup
            RunModePopup.IsOpen = false;

            // 标记事件已处理，防止冒泡导致其他问题
            e.Handled = true;
        }

        /// <summary>
        /// 查找父元素
        /// </summary>
        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }



        /// <summary>
        /// 属性更改通知
        /// </summary>
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 加载图像（已废弃，请使用LoadImageOptimized）
        /// </summary>
        /// <summary>
        /// 更新加载范围（滚动时调用）
        /// 使用优先级加载器
        /// ◆虚拟化安全：使用 ItemContainerGenerator 精确获取可视范围
        /// </summary>
        private void UpdateLoadRange()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            if (_thumbnailListBox == null || ImageCollection == null || ImageCollection.Count == 0)
            {
return;
            }

            // ◆使用虚拟化安全的方法获取可视范围
            var (firstVisible, lastVisible) = GetVisibleRange();
            
            if (firstVisible == -1 || lastVisible == -1)
            {
return;
            }

            var viewportWidth = _thumbnailListBox.ActualWidth;
            
            // ===== 缩略图显示状态监控（增强诊断） =====
            int loadedCount = 0, emptyCount = 0, validCount = 0, invalidCount = 0;
            var emptyIndices = new System.Text.StringBuilder();
            var invalidDetails = new System.Text.StringBuilder();
            
            for (int i = firstVisible; i <= lastVisible && i < ImageCollection.Count; i++)
            {
                var item = ImageCollection[i];
                if (item.Thumbnail != null)
                {
                    loadedCount++;
                    // ◆诊断：检查缩略图是否真的有效（有宽高）
                    if (item.Thumbnail.Width > 0 && item.Thumbnail.Height > 0)
                    {
                        validCount++;
                    }
                    else
                    {
                        invalidCount++;
                        if (invalidDetails.Length < 200)
                            invalidDetails.Append($"[{i}:W{item.Thumbnail.Width:F0}xH{item.Thumbnail.Height:F0}]");
                    }
                }
                else
                {
                    emptyCount++;
                    if (emptyIndices.Length < 100) // 限制长度
                        emptyIndices.Append($"{i},");
                }
            }
            
            // 缩略图状态监控日志（仅当存在未加载项时输出）
            if (emptyCount > 0 || invalidCount > 0)
            {
                var diag = $"可视区域[{firstVisible}-{lastVisible}] 已加载={loadedCount} 为空={emptyCount} 无效={invalidCount}";
                if (emptyIndices.Length > 0)
                    diag += $" 空索引=[{emptyIndices}]";
                if (invalidDetails.Length > 0)
                    diag += $" 无效详情=[{invalidDetails}]";
                VisionLogger.Instance.Warning($"[ImagePreview] {diag}", "ImagePreviewControl");
            }

            // 委托给优先级加载器
            var collection = ImageCollection;
            if (collection != null && collection.Count > 0)
            {
                _priorityLoader.UpdateVisibleRange(firstVisible, lastVisible, collection.Count);
            }
            
            sw.Stop();
        }

        #endregion

        #region 事件

        /// <summary>
        /// 获取当前可视区域内的数据项索引范围（虚拟化安全版本）
        /// 核心原理：虚拟化模式下 ScrollViewer 的 HorizontalOffset 返回的是"项索引"而非像素
        /// </summary>
        /// <returns>返回(firstVisible, lastVisible)，如果找不到返回(-1, -1)</returns>
        private (int firstVisible, int lastVisible) GetVisibleRange()
        {
            // ===== 诊断日志：检查前置条件 =====
            var callStack = new System.Diagnostics.StackTrace();
            var caller = callStack.GetFrame(1)?.GetMethod()?.Name ?? "unknown";
            
            if (_thumbnailListBox == null)
            {
return (-1, -1);
            }
            
            if (ImageCollection == null || ImageCollection.Count == 0)
            {
return (-1, -1);
            }

            var scrollViewer = FindVisualChild<ScrollViewer>(_thumbnailListBox);
            if (scrollViewer == null)
            {
                VisionLogger.Instance.Warning(
                    $"[ScrollDiag] GetVisibleRange: FindVisualChild<ScrollViewer> 返回 null, " +
                    $"caller={caller}, listBox.ChildrenCount={VisualTreeHelper.GetChildrenCount(_thumbnailListBox)}",
                    "ImagePreviewControl");
return (-1, -1);
            }

            double itemWidth = ThumbnailSizes.ItemWidth;
            if (itemWidth <= 0)
            {
return (-1, -1);
            }

            // VirtualizingStackPanel + CanContentScroll="True"（默认值）
            // HorizontalOffset = 第一项索引
            // ViewportWidth = 可见项数
            double offset = scrollViewer.HorizontalOffset;
            double scrollViewportWidth = scrollViewer.ViewportWidth;

            int visibleCount = (int)Math.Ceiling(scrollViewportWidth);
            int firstVisible = Math.Max(0, (int)offset);
            int lastVisible = Math.Min(ImageCollection.Count - 1, firstVisible + visibleCount - 1);

            // ±1 张缓冲（防止快速滚动时短暂白屏）
            firstVisible = Math.Max(0, firstVisible - 1);
            lastVisible = Math.Min(ImageCollection.Count - 1, lastVisible + 1);

            return (firstVisible, lastVisible);
        }

        /// <summary>
        /// 内存压力变化事件处理（P0优化：自适应策略）
        /// </summary>
        private void OnMemoryPressureChanged(object? sender, MemoryPressureMonitor.MemoryPressureEventArgs e)
        {
            // ◆P1优化：通知加载器调整并发度
            _priorityLoader.SetMemoryPressure(e.Level);

            // 根据压力级别自适应响应
            switch (e.Level)
            {
                case MemoryPressureMonitor.PressureLevel.Moderate:
                    // 中等压力：减少预读取，保持稳定
                    s_smartLoader.ClearPrefetchCache(); // 清除预读取缓存
                    // ◆方案A: 同步状态
                    _priorityLoader.SyncLoadedIndicesWithActualThumbnails();
break;

                case MemoryPressureMonitor.PressureLevel.High:
                    // 高压力：清理缓存，降低质量
                    s_thumbnailCache.RespondToMemoryPressure(isCritical: false);
                    s_smartLoader.ClearPrefetchCache();
                    // ◆方案A: 同步状态
                    _priorityLoader.SyncLoadedIndicesWithActualThumbnails();
break;

                case MemoryPressureMonitor.PressureLevel.Critical:
                    // 危险：强制GC，清空缓存，持续低质量模式
                    s_thumbnailCache.RespondToMemoryPressure(isCritical: true);
                    s_smartLoader.ClearPrefetchCache();
                    // ◆方案A: 同步状态
                    _priorityLoader.SyncLoadedIndicesWithActualThumbnails();
                    
                    // 触发GC回收
                    Task.Run(() =>
                    {
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    });
break;

                case MemoryPressureMonitor.PressureLevel.Normal:
                    // 恢复正常：恢复高质量模式
break;
            }
        }

        /// <summary>
        /// INotifyPropertyChanged接口实现
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion
    }

    /// <summary>
    /// 图像运行模式显示转换器
    /// </summary>
    public class ImageRunModeDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "运行全部";

            // 尝试直接转换为ImageRunMode
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
    /// Boolean到Brush转换器（用于自动切换按钮）
    /// </summary>
    public class BooleanToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked)
            {
                return isChecked ? new SolidColorBrush(Colors.Orange) : new SolidColorBrush(Color.FromRgb(192, 192, 192));
            }
            return new SolidColorBrush(Color.FromRgb(192, 192, 192));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 反向布尔转换器
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }

    /// <summary>
    /// Null到Opacity转换器（用于控制加载动画的显示/隐藏）
    /// </summary>
    public class NullToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? 1.0 : 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
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
