using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Services.Performance;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Views.Controls.Panels;

namespace SunEyeVision.UI.Services.Thumbnail
{
    /// <summary>
    /// 加载优先级枚举?
    /// </summary>
    public enum LoadPriority
    {
        /// <summary>关键优先?- 首张图片立即显示</summary>
        Critical = 0,
        /// <summary>高优先级 - 可见区域图片</summary>
        High = 1,
        /// <summary>中等优先?- 预加载区域?/summary>
        Medium = 2,
        /// <summary>低优先级 - 后台空闲加载</summary>
        Low = 3,
        /// <summary>空闲优先?- 仅在空闲时加载，可随时取消?/summary>
        Idle = 4
    }

    /// <summary>
    /// 缓冲区域类型
    /// </summary>
    public enum BufferZone
    {
        /// <summary>活跃区域 - 当前可视范围，最高优先级</summary>
        Active,
        /// <summary>预加载区域?- 可视范围边缘，中等优先级</summary>
        Prefetch,
        /// <summary>后备区域 - 远离可视范围，空闲时加载</summary>
        Reserve
    }

    /// <summary>
    /// 滚动类型枚举
    /// </summary>
    public enum ScrollType
    {
        /// <summary>停止滚动</summary>
        Stopped,
        /// <summary>慢速滚动?/summary>
        Slow,
        /// <summary>快速滚动?/summary>
        Fast,
        /// <summary>超快滚动（跳过预加加载?/summary>
        UltraFast
    }

    /// <summary>
    /// 动态加载策略配置?
    /// ?单一数据源架构：所有值基于实际可视数量动态计算?
    /// 核心设计：限制总入队数量，避免1000+张图片全入队
    /// 动态范围?= 可视区域 + 缓冲区域 + 预测区域
    /// </summary>
    public class DynamicLoadingPolicy
    {
        /// <summary>实际可视区域大小（由外部设置，基于GetVisibleRange()?/summary>
        public int ActualVisibleSize { get; private set; } = 10; // 默认值，首屏加载前会被更新?
        
        /// <summary>缓冲区域倍数（可视区域?× 此值）</summary>
        /// <remarks>?方案B优化：从1.0降到0.5，减少首屏入队数据?/remarks>
        public double BufferMultiplier { get; set; } = 0.5;
        
        /// <summary>预测区域倍数（可视区域?× 此值）</summary>
        /// <remarks>?方案B优化：从3.0降到1.0，大幅减少首屏入队数据?/remarks>
        public double PrefetchMultiplier { get; set; } = 1.0;
        
        /// <summary>快速滚动阈值（?秒）</summary>
        public double FastScrollThreshold { get; set; } = 5.0;
        
        /// <summary>超快滚动阈值（?秒）</summary>
        public double UltraFastScrollThreshold { get; set; } = 15.0;
        
        /// <summary>最大预加载范围（限制总入队数量）</summary>
        public int MaxPrefetchRange { get; set; } = 200;
        
        /// <summary>
        /// ?单一数据源：设置实际可视数量，所有派生值自动更新?
        /// </summary>
        public void SetActualVisibleSize(int visibleSize)
        {
            ActualVisibleSize = Math.Max(1, visibleSize);
        }
        
        /// <summary>可视区域大小（派生自ActualVisibleSize?/summary>
        public int VisibleSize => ActualVisibleSize;
        
        /// <summary>缓冲区域大小（派生：可视区域 × BufferMultiplier?/summary>
        public int BufferSize => (int)(ActualVisibleSize * BufferMultiplier);
        
        /// <summary>预测区域大小（派生：可视区域 × PrefetchMultiplier?/summary>
        public int PrefetchSize => (int)(ActualVisibleSize * PrefetchMultiplier);
        
        /// <summary>
        /// 计算动态范围总数
        /// 动态范围?= 可视区域 + 缓冲区域 + 预测区域
        /// </summary>
        public int TotalDynamicRange => VisibleSize + BufferSize + PrefetchSize;
        
        /// <summary>
        /// 根据滚动类型获取预加载倍数
        /// </summary>
        public double GetPrefetchMultiplier(ScrollType scrollType)
        {
            return scrollType switch
            {
                ScrollType.Stopped => 1.0,
                ScrollType.Slow => 1.5,
                ScrollType.Fast => 2.0,
                ScrollType.UltraFast => 0.5, // 超快滚动时减少预加载
                _ => 1.0
            };
        }
    }

    /// <summary>
    /// 缓冲区域配置
    /// ?单一数据源架构：所有值基于实际可视数量动态派?
    /// </summary>
    public class BufferZoneConfig
    {
        private int _actualVisibleSize = 10; // 由外部设?
        
        /// <summary>
        /// ?单一数据源：设置实际可视数量
        /// </summary>
        public void SetActualVisibleSize(int visibleSize)
        {
            _actualVisibleSize = Math.Max(1, visibleSize);
        }
        
        /// <summary>活跃区域大小 = 实际可视范围（派生）</summary>
        public int ActiveSize => _actualVisibleSize;
        
        /// <summary>预加载区域大小（单侧，派生：可视区域?0%?/summary>
        public int PrefetchSize => Math.Max(3, _actualVisibleSize / 2);
        
        /// <summary>后备区域大小（单侧，派生：可视区域的3倍）</summary>
        public int ReserveSize => _actualVisibleSize * 3;
        
        /// <summary>快速滚动时预加载区域倍增因子</summary>
        public double FastScrollPrefetchMultiplier { get; set; } = 2.0;
        
        /// <summary>静止时后备区域激活延?ms)</summary>
        public int ReserveActivationDelay { get; set; } = 2000;
    }

    /// <summary>
    /// 加载任务
    /// </summary>
    public class LoadTask : IComparable<LoadTask>
    {
        public int Index { get; set; }
        public LoadPriority Priority { get; set; }
        public DateTime EnqueueTime { get; set; }
        public string FilePath { get; set; } = string.Empty;

        public int CompareTo(LoadTask? other)
        {
            if (other == null) return 1;
            // 优先级高的排前面（数值小的优先级高）
            int priorityCompare = Priority.CompareTo(other.Priority);
            if (priorityCompare != 0) return priorityCompare;
            // 同优先级按索引排序?
            return Index.CompareTo(other.Index);
        }
    }

    /// <summary>
    /// 优先级缩略图加加载?- 单一加载系统，消除双系统协调问题
    /// 
    /// 核心设计算?
    /// 1. 使用优先级队列替代双加载系统
    /// 2. 无pause/resume机制，通过优先级自然调试?
    /// 3. 首屏和滚动加载统一处理
    /// </summary>
    public class PriorityThumbnailLoader : IDisposable
    {
        #region 字段

        // 缩略图加载委托?
        private Func<string, int, bool, BitmapImage?>? _loadThumbnailFunc;
        private Action<string, BitmapImage?>? _addToCacheAction;
        
        // ?实时获取可视范围的委托（解决可视范围缓存过时问题?
        private Func<(int first, int last)>? _getVisibleRangeFunc;

        // 优先级队列（线程安全局?
        private readonly PriorityQueue<LoadTask, int> _loadQueue = new PriorityQueue<LoadTask, int>();
        private readonly HashSet<int> _queuedIndices = new HashSet<int>(); // 已入队索引?
        private readonly HashSet<int> _loadedIndices = new HashSet<int>(); // 已加载索引（Thumbnail已成功设置）
        private readonly HashSet<int> _decodingIndices = new HashSet<int>(); // ?新增：正在解码中的索引?
        private readonly object _queueLock = new object();

        // 任务管理
        private Task? _loadTask;
        private CancellationTokenSource? _cancellationTokenSource;
        private int _activeTaskCount = 0;
        
        // 简化架构：统一线程池，与 WicGpuDecoder 并发限制(8)匹配
        private readonly SemaphoreSlim _loadSemaphore;
        private const int MAX_CONCURRENT_LOADS = 8;

        // 集合引用（支持批量操作集合）
        private BatchObservableCollection<ImageInfo>? _imageCollection;

        // 性能监控
        private readonly PerformanceLogger _logger = new PerformanceLogger("PriorityLoader");
        private readonly Queue<long> _decodeTimes = new Queue<long>();
        private int _dynamicConcurrency = 4;
        private DateTime _lastConcurrencyAdjust = DateTime.MinValue;
        private DateTime _startupTime = DateTime.MinValue; // ?新增：启动时间，用于首屏延迟调整
        private const int MIN_CONCURRENCY = 4; // ?优化：提高最小并发度，避免过渡?
        private const int MAX_CONCURRENCY = 8;
        private const int STARTUP_GRACE_PERIOD_MS = 3000; // ?新增：首屏并发调整延??

        // ?P1优化：内存压力级?
        private MemoryPressureMonitor.PressureLevel _currentMemoryPressure = MemoryPressureMonitor.PressureLevel.Normal;

        // 滚动状态追?
        private int _lastFirstVisible = -1;
        private int _lastLastVisible = -1;
        private int _scrollDirection = 0; // 1=向右, -1=向左, 0=静止
        private double _scrollVelocity = 0; // ?P1优化：滚动速度（项/秒）
        private DateTime _lastScrollTime = DateTime.MinValue;
        private bool _isFastScrolling = false;
        private ScrollType _currentScrollType = ScrollType.Stopped; // ?动态范围：滚动类型

        // ?动态范围加载策略?
        private readonly DynamicLoadingPolicy _dynamicPolicy = new DynamicLoadingPolicy();

        // ?P2优化：缓冲区域管?
        private readonly BufferZoneConfig _bufferConfig = new BufferZoneConfig();
        private DateTime _lastScrollStopTime = DateTime.MinValue;
        private int _currentPrefetchSize = 10;
        private DispatcherTimer? _reserveActivationTimer;

        // 缓冲区域范围缓存
        private (int start, int end) _activeZone = (0, 0);
        private (int start, int end) _prefetchZoneLeft = (0, 0);
        private (int start, int end) _prefetchZoneRight = (0, 0);
        private (int start, int end) _reserveZoneLeft = (0, 0);
        private (int start, int end) _reserveZoneRight = (0, 0);

        // UI批量更新
        private readonly ConcurrentQueue<UIUpdateRequest> _uiUpdateQueue = new ConcurrentQueue<UIUpdateRequest>();
        private DispatcherTimer? _uiUpdateTimer;

        // 动态质?
        private bool _useLowQuality = false;

        // 可视区域加载监控
        private DateTime? _visibleAreaLoadStartTime = null;
        private int _visibleAreaCount = 0;
        private int _loadedInVisibleArea = 0;

        // 清理阈?
        private const int CLEANUP_THRESHOLD = 50;
        private const int CLEANUP_KEEP_MARGIN = 30;

        private bool _disposed = false;

        #endregion

        #region 属性?

        /// <summary>是否使用低质量缩略图（快速滚动时间?/summary>
        public bool UseLowQuality
        {
            get => _useLowQuality;
            set
            {
                if (_useLowQuality != value)
                {
                    _useLowQuality = value;
}
            }
        }

        /// <summary>当前活动任务?/summary>
        public int ActiveTaskCount => _activeTaskCount;

        /// <summary>队列中待加载数量</summary>
        public int PendingCount
        {
            get
            {
                lock (_queueLock)
                {
                    return _loadQueue.Count;
                }
            }
        }

        /// <summary>已加载数据?/summary>
        public int LoadedCount
        {
            get
            {
                lock (_queueLock)
                {
                    return _loadedIndices.Count;
                }
            }
        }

        /// <summary>是否快速滚动中</summary>
        public bool IsFastScrolling => _isFastScrolling;

        /// <summary>当前缓冲区域配置（只读）</summary>
        public BufferZoneConfig BufferConfig => _bufferConfig;

        #endregion

        #region 事件

        /// <summary>可视区域加载完成事件</summary>
        public event Action<int, TimeSpan>? VisibleAreaLoadingCompleted;

        #endregion

        #region 构造函数

        public PriorityThumbnailLoader()
        {
            // 简化架构：统一线程池，与 WicGpuDecoder 并发限制(8)匹配
            _loadSemaphore = new SemaphoreSlim(MAX_CONCURRENT_LOADS);
}

        #endregion

        #region 公共方法

        /// <summary>
        /// 取消并重置所有加载任务?
        /// ?首屏优化：在加载新目录前清理旧任务，防止资源竞争
        /// </summary>
        public void CancelAndReset()
        {
            var diagSw = Stopwatch.StartNew();
            
            // 取消当前加载任务
            _cancellationTokenSource?.Cancel();
            diagSw.Stop();
            long cancelMs = diagSw.ElapsedMilliseconds;
            
            // 等待一小段时间让任务响应取消?
            diagSw.Restart();
            Thread.Sleep(10);
            diagSw.Stop();
            long sleepMs = diagSw.ElapsedMilliseconds;
            
            // 清空队列
            diagSw.Restart();
            lock (_queueLock)
            {
                _loadQueue.Clear();
                _queuedIndices.Clear();
                _decodingIndices.Clear();
                _loadedIndices.Clear();
            }
            diagSw.Stop();
            long clearQueueMs = diagSw.ElapsedMilliseconds;
            
            // 清空UI更新队列
            diagSw.Restart();
            while (_uiUpdateQueue.TryDequeue(out _)) { }
            diagSw.Stop();
            long clearUiQueueMs = diagSw.ElapsedMilliseconds;
            
            // 重置状态?
            _loadedInVisibleArea = 0;
            _visibleAreaCount = 0;
        }

        /// <summary>
        /// 首屏加载 - 单一数据源架?
        /// ?核心优化：基于实际可视数量动态计算加载范围?
        /// 
        /// 数据流（统一）：
        /// 1. GetVisibleRange() ?实际可视数量（如10张）
        /// 2. 可视区域加载 ?入队10张（Critical/High?
        /// 3. 可视区域完成判断 ?检查?0张是否全部加?
        /// </summary>
        public async Task LoadInitialScreenAsync(
            string[] fileNames,
            BatchObservableCollection<ImageInfo> imageCollection,
            Action<int>? onFirstImageLoaded = null)
        {
            // ===== ?诊断计时：总计时开?=====
            var totalDiagSw = Stopwatch.StartNew();
            
            // ===== ?首屏优化：取消之前的所有加载任务?=====
            // 防止旧任务与Critical任务竞争资源
            var stepSw = Stopwatch.StartNew();
            CancelAndReset();
            stepSw.Stop();
            long cancelAndResetMs = stepSw.ElapsedMilliseconds;
            
            _imageCollection = imageCollection;
            
            // ?新增：记录启动时间，用于首屏并发调整延迟
            _startupTime = DateTime.Now;

            // ===== ?内存预判：评估系统内存压缩?=====
            stepSw.Restart();
            var memoryInfo = GC.GetGCMemoryInfo();
            long availableMemoryMB = memoryInfo.TotalAvailableMemoryBytes / (1024 * 1024);
            long memoryLoadPercent = 100 - (memoryInfo.TotalAvailableMemoryBytes * 100 / memoryInfo.MemoryLoadBytes);
            bool isMemoryConstrained = availableMemoryMB < 2000 || memoryLoadPercent > 70;
            stepSw.Stop();
            long memoryCheckMs = stepSw.ElapsedMilliseconds;
            
            if (isMemoryConstrained)
            {
}

            // ===== ?单一数据源：获取实际可视数量 =====
            stepSw.Restart();
            int visibleCount = CalculateVisibleCount();
            
            // ?如果委托返回无效值，等待UI布局完成后重置?
            if (visibleCount <= 1 && _getVisibleRangeFunc != null)
            {
                // UI可能还没布局完成，延迟获?
                await Task.Delay(50);
                visibleCount = CalculateVisibleCount();
            }
            stepSw.Stop();
            long calcVisibleMs = stepSw.ElapsedMilliseconds;
            
            // 确保至少加载1?
            visibleCount = Math.Max(1, visibleCount);
            int loadCount = Math.Min(visibleCount, fileNames.Length);

            // ?内存预判：动态调整预加载数量
            stepSw.Restart();
            int bufferSize, prefetchSize, totalEnqueueCount;
            if (isMemoryConstrained)
            {
                // 内存紧张：减少预加载
                bufferSize = Math.Min(_dynamicPolicy.BufferSize / 2, fileNames.Length - visibleCount);
                prefetchSize = Math.Min(_dynamicPolicy.PrefetchSize / 3, fileNames.Length - visibleCount - bufferSize);
                totalEnqueueCount = Math.Min(visibleCount + bufferSize + prefetchSize, fileNames.Length);
}
            else
            {
                // 正常策略
                int visibleSize = Math.Min(_dynamicPolicy.VisibleSize, fileNames.Length);
                bufferSize = Math.Min(_dynamicPolicy.BufferSize, fileNames.Length - visibleSize);
                prefetchSize = Math.Min(_dynamicPolicy.PrefetchSize, fileNames.Length - visibleSize - bufferSize);
                totalEnqueueCount = Math.Min(visibleSize + bufferSize + prefetchSize, fileNames.Length);
            }
            stepSw.Stop();
            long policyCalcMs = stepSw.ElapsedMilliseconds;

            // 设置可视区域范围（首屏加载时间点开始）。
            _lastFirstVisible = 0;
            _lastLastVisible = loadCount - 1;
            _visibleAreaLoadStartTime = DateTime.Now;
            _visibleAreaCount = loadCount;  // 统一：使用实际可视数据。
            _loadedInVisibleArea = 0;

            // ===== 诊断日志：前置准备阶段 =====
            totalDiagSw.Stop();
// ===== ?P0优化：Critical任务直接同步执行，消除线程池冷启动开销 =====
            // 首张图片不入队，直接同步解码
            long criticalDecodeMs = 0;
            long uiUpdateMs = 0;
            long callbackMs = 0;
            
            if (fileNames.Length > 0 && imageCollection.Count > 0)
            {
                string firstFileName = System.IO.Path.GetFileName(fileNames[0]);
// ?P0优化：直接同步执行，无线程池调度开销
                // ?优先级感知：Critical任务使用高优先级GPU解码
                stepSw.Restart();
                var thumbnail = LoadThumbnailOptimized(fileNames[0], 60, isHighPriority: true);
                stepSw.Stop();
                criticalDecodeMs = stepSw.ElapsedMilliseconds;
                
                if (thumbnail != null)
                {
                    // ?关键优化：使用Normal优先级，避免被后台任务饿?
                    // Loaded优先级太低，在大量后台任务时会延?-2?
                    stepSw.Restart();
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // ?修复：竞态条件防护 - 异步执行时集合可能已变化
                        if (imageCollection.Count > 0)
                        {
                            imageCollection[0].Thumbnail = thumbnail;
                        }
                        else
                        {
}
                    }, DispatcherPriority.Normal);
                    stepSw.Stop();
                    uiUpdateMs = stepSw.ElapsedMilliseconds;
                    
                    // 添加到缓存?
                    _addToCacheAction?.Invoke(fileNames[0], thumbnail);
                    
                    // 标记已加?
                    lock (_queueLock)
                    {
                        _loadedIndices.Add(0);
                    }
                    
                    Interlocked.Increment(ref _loadedInVisibleArea);
                    
                    // ?修复：使用实际解码时间判断加载方法?
                    string loadMethod = criticalDecodeMs < 10 ? "缓存命中" : "GPU解码";
// 立即回调通知首张图片已加?
                    stepSw.Restart();
                    onFirstImageLoaded?.Invoke(0);
                    stepSw.Stop();
                    callbackMs = stepSw.ElapsedMilliseconds;
                    
                    // ===== ?诊断日志：Critical阶段 =====
}
                else
                {
}
            }

            // 第一批：可视区域剩余部分（High）从index=1开始
            // ?修复：使用loadCount而非visibleCount，防止fileNames越界
            for (int i = 1; i < loadCount; i++)
            {
                EnqueueLoadTask(i, fileNames[i], LoadPriority.High);
            }

            // 第二批：缓冲区域（Medium）预加载边框
            // ?修复：添加fileNames边界检查
            int bufferEnd = Math.Min(visibleCount + bufferSize, fileNames.Length);
            for (int i = visibleCount; i < bufferEnd; i++)
            {
                EnqueueLoadTask(i, fileNames[i], LoadPriority.Medium);
            }

            // 第三批：预测区域（Low）后台预加载
            // ?修复：添加fileNames边界检查
            int prefetchEnd = Math.Min(totalEnqueueCount, fileNames.Length);
            for (int i = visibleCount + bufferSize; i < prefetchEnd; i++)
            {
                EnqueueLoadTask(i, fileNames[i], LoadPriority.Low);
            }
            // 启动后台加载任务（处理剩余任务）
            StartLoading();
        }

        /// <summary>
        /// 更新可视区域 - 滚动时调试?
        /// ?单一数据源架构：使用实际可视数量进行判断
        /// </summary>
        public void UpdateVisibleRange(int firstVisible, int lastVisible, int totalCount)
        {
            var sw = Stopwatch.StartNew();

            if (totalCount <= 0 || _imageCollection == null) return;

            // ?单一数据源：更新所有配置的实际可视数量
            int actualVisibleCount = lastVisible - firstVisible + 1;
            _dynamicPolicy.SetActualVisibleSize(actualVisibleCount);
            _bufferConfig.SetActualVisibleSize(actualVisibleCount);

            // ?P2优化：更新滚动状态并计算缓冲区域
            UpdateScrollState(firstVisible, lastVisible, totalCount);

            // ?方案C：在检查可视区域状态前，先同步该区域内的状态?
            int fixedCount = SyncVisibleAreaState(firstVisible, lastVisible);

            // ?单一数据源：使用实际可视数量进行状态监控?
            int visibleCount = actualVisibleCount;  // 统一使用实际?
            int needLoadCount = 0;
            int alreadyLoadedCount = 0;
            int alreadyQueuedCount = 0;
            
            lock (_queueLock)
            {
                for (int i = firstVisible; i <= lastVisible; i++)
                {
                    if (_loadedIndices.Contains(i))
                        alreadyLoadedCount++;
                    else if (_queuedIndices.Contains(i))
                        alreadyQueuedCount++;
                    else
                        needLoadCount++;
                }
            }

            // 可视区域加载状态监控（静默处理?

            if (needLoadCount > 0 && _visibleAreaLoadStartTime == null)
            {
                _visibleAreaLoadStartTime = DateTime.Now;
                _visibleAreaCount = visibleCount;  // ?统一：使用实际可视数据?
                _loadedInVisibleArea = 0;
            }

            // ?P2优化：根据缓冲区域入队任务?
            int actualCount = _imageCollection?.Count ?? 0;
            int enqueuedCount = 0;
            int prefetchEnqueued = 0;

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                if (i >= 0 && i < actualCount && i < _imageCollection.Count)
                {
                    var filePath = _imageCollection[i].FilePath;
                    var priority = i == firstVisible ? LoadPriority.Critical : LoadPriority.High;
                    if (EnqueueLoadTaskWithLog(i, filePath, priority))
                        enqueuedCount++;
                }
            }

            // 预加载区域（Medium 优先级）
            if (_scrollDirection >= 0)
            {
                for (int i = _prefetchZoneRight.start; i <= _prefetchZoneRight.end && i < actualCount; i++)
                {
                    if (i >= 0 && i < _imageCollection.Count)
                    {
                        var filePath = _imageCollection[i].FilePath;
                        EnqueueLoadTask(i, filePath, LoadPriority.Medium);
                        prefetchEnqueued++;
                    }
                }
            }
            if (_scrollDirection <= 0)
            {
                for (int i = _prefetchZoneLeft.start; i <= _prefetchZoneLeft.end && i < actualCount; i++)
                {
                    if (i >= 0 && i < _imageCollection.Count)
                    {
                        var filePath = _imageCollection[i].FilePath;
                        EnqueueLoadTask(i, filePath, LoadPriority.Medium);
                        prefetchEnqueued++;
                    }
                }
            }

            int loadedCount, queuedCount, decodingCount;
            lock (_queueLock)
            {
                loadedCount = _loadedIndices.Count;
                queuedCount = _queuedIndices.Count;
                decodingCount = _decodingIndices.Count;
            }
            VisionLogger.Instance.Info(
                $"[LoadDiag] UpdateVisibleRange range=[{firstVisible}-{lastVisible}] " +
                $"needLoad={needLoadCount} loaded={alreadyLoadedCount} queued={alreadyQueuedCount} " +
                $"enqueued={enqueuedCount} prefetch={prefetchEnqueued} " +
                $"loadedIndices={loadedCount} queued={queuedCount} decoding={decodingCount}",
                "PriorityThumbnailLoader");

            // 清理远离可视区域的缩略图
            CleanupOutOfRangeThumbnails(firstVisible, lastVisible, totalCount);

            // 启动加载
            StartLoading();

            sw.Stop();
        }

        /// <summary>
        /// ?P0优化：立即更新可视区域（不等防抖?
        /// ?动态范围：超快滚动时只入队可视区域，不预加?
        /// </summary>
        public void UpdateVisibleRangeImmediate(int firstVisible, int lastVisible, int totalCount)
        {
            if (totalCount <= 0 || _imageCollection == null) return;

            // 同步可视区域状态?
            SyncVisibleAreaState(firstVisible, lastVisible);

            // ?动态范围：超快滚动时只入队可视区域
            int actualCount = _imageCollection?.Count ?? 0;
            int enqueuedCount = 0;
            int skippedCount = 0;

            if (_currentScrollType == ScrollType.UltraFast)
            {
                // 超快滚动：只入队首张（Critical?
                if (firstVisible >= 0 && firstVisible < actualCount && firstVisible < _imageCollection.Count)
                {
                    var filePath = _imageCollection[firstVisible].FilePath;
                    if (EnqueueLoadTaskWithLog(firstVisible, filePath, LoadPriority.Critical))
                        enqueuedCount++;
                    else
                        skippedCount++;
                }
            }
            else
            {
                // 正常/快速滚动：入队可视区域（High优先级）
                for (int i = firstVisible; i <= lastVisible; i++)
                {
                    if (i >= 0 && i < actualCount && i < _imageCollection.Count)
                    {
                        var filePath = _imageCollection[i].FilePath;
                        var priority = i == firstVisible ? LoadPriority.Critical : LoadPriority.High;
                        if (EnqueueLoadTaskWithLog(i, filePath, priority))
                            enqueuedCount++;
                        else
                            skippedCount++;
                    }
                }
            }

            int loadedCount, queuedCount, decodingCount;
            lock (_queueLock)
            {
                loadedCount = _loadedIndices.Count;
                queuedCount = _queuedIndices.Count;
                decodingCount = _decodingIndices.Count;
            }
            VisionLogger.Instance.Info(
                $"[LoadDiag] ImmediateUpdate range=[{firstVisible}-{lastVisible}] " +
                $"enqueued={enqueuedCount} skipped={skippedCount} " +
                $"loadedIndices={loadedCount} queued={queuedCount} decoding={decodingCount}",
                "PriorityThumbnailLoader");

            // 启动加载
            StartLoading();
        }

        /// <summary>
        /// 同步可视区域内的状态（解决 _loadedIndices ?Thumbnail 不一致）
        /// ?方案C核心：滚动时主动修复状态?
        /// </summary>
        private int SyncVisibleAreaState(int firstVisible, int lastVisible)
        {
            // ?修复竞态条件：捕获集合引用
            var imageCollection = _imageCollection;
            if (imageCollection == null) return 0;

            int fixedCount = 0;
            lock (_queueLock)
            {
                for (int i = firstVisible; i <= lastVisible && i < imageCollection.Count; i++)
                {
                    // 检查：_loadedIndices 标记已加载，?Thumbnail 实际?null
                    if (_loadedIndices.Contains(i) && imageCollection[i].Thumbnail == null)
                    {
                        _loadedIndices.Remove(i);
                        _queuedIndices.Remove(i);
                        _decodingIndices.Remove(i);  // ?新增：清理解码中索引
                        fixedCount++;
                    }
                    
                    // ?新增：检查解码超时（超过5秒仍在解码中，可能卡住）
                    if (_decodingIndices.Contains(i) && imageCollection[i].Thumbnail == null)
                    {
                        // 如果已经在解码中?Thumbnail 为空，检查是否需要重新加?
                        // 这里不做处理，让自然超时机制处理
                    }
                }
            }

            return fixedCount;
        }

        /// <summary>
        /// 清空加载状态（加载新文件夹时调用）
        /// </summary>
        public void ClearState()
        {
            lock (_queueLock)
            {
                _loadQueue.Clear();
                _queuedIndices.Clear();
                _loadedIndices.Clear();
                _decodingIndices.Clear();  // ?新增：清理解码中索引
            }

            while (_uiUpdateQueue.TryDequeue(out _)) { }

            _lastFirstVisible = -1;
            _lastLastVisible = -1;
            _visibleAreaLoadStartTime = null;
            _visibleAreaCount = 0;
            _loadedInVisibleArea = 0;

            // 状态清空不输出日志
        }

        /// <summary>
        /// 标记指定索引为未加载状态（容器回收时调用）
        /// ?关键：解决状态不一致问题?
        /// </summary>
        public void MarkAsUnloaded(int index)
        {
            lock (_queueLock)
            {
                _loadedIndices.Remove(index);
                _queuedIndices.Remove(index);
                _decodingIndices.Remove(index);  // ?新增：清理解码中索引
            }
        }

        /// <summary>
        /// 同步已加载索引与实际缩略图状态?
        /// ?关键：解决缓存清理后状态不一致问题?
        /// 当内存压力导致缓存被清理时，_loadedIndices 可能与实例?Thumbnail 状态不同步
        /// </summary>
        public void SyncLoadedIndicesWithActualThumbnails()
        {
            // ?修复竞态条件：捕获集合引用
            var imageCollection = _imageCollection;
            if (imageCollection == null) return;

            var toRemove = new List<int>();
            lock (_queueLock)
            {
                foreach (var index in _loadedIndices)
                {
                    if (index < 0 || index >= imageCollection.Count)
                    {
                        toRemove.Add(index);
                    }
                    else if (imageCollection[index].Thumbnail == null)
                    {
                        // Thumbnail已被GC清理，但索引还在已加载集合中
                        toRemove.Add(index);
                    }
                }

                foreach (var index in toRemove)
                {
                    _loadedIndices.Remove(index);
                    _queuedIndices.Remove(index);
                    _decodingIndices.Remove(index);  // ?新增：清理解码中索引
                }
            }

            if (toRemove.Count > 0)
            {
}
        }

        /// <summary>
        /// 设置图像集合引用
        /// </summary>
        public void SetImageCollection(BatchObservableCollection<ImageInfo> collection)
        {
            _imageCollection = collection;
            
            // _getVisibleRangeFunc 由 SetLoadThumbnailFunc 设置一次后永久有效
        }

        /// <summary>
        /// 设置缩略图加载函数（委托模式?
        /// </summary>
        public void SetLoadThumbnailFunc(Func<string, int, bool, BitmapImage?> loadFunc, Action<string, BitmapImage?>? addToCacheAction = null, Func<(int first, int last)>? getVisibleRangeFunc = null)
        {
            _loadThumbnailFunc = loadFunc;
            _addToCacheAction = addToCacheAction;
            _getVisibleRangeFunc = getVisibleRangeFunc;
        }

        /// <summary>
        /// ?P1优化：根据内存压力动态调整并发度
        /// ?ImagePreviewControl 中连接内存监控事件时调用
        /// </summary>
        public void SetMemoryPressure(MemoryPressureMonitor.PressureLevel level)
        {
            _currentMemoryPressure = level;
            
            // 根据压力调整并发布?
            _dynamicConcurrency = level switch
            {
                MemoryPressureMonitor.PressureLevel.Normal => 8,
                MemoryPressureMonitor.PressureLevel.Moderate => 4,
                MemoryPressureMonitor.PressureLevel.High => 2,
                MemoryPressureMonitor.PressureLevel.Critical => 1,
                _ => 4
            };
            
            // 高压力时取消低优先级任务
            if (level >= MemoryPressureMonitor.PressureLevel.High)
            {
                CancelLowPriorityTasks();
                _useLowQuality = true;
            }
            else
            {
                _useLowQuality = false;
            }
}

        /// <summary>
        /// ?P1优化：取消低优先级任务?
        /// </summary>
        private void CancelLowPriorityTasks()
        {
            lock (_queueLock)
            {
                var toRemove = new List<int>();
                var tempQueue = new PriorityQueue<LoadTask, int>();
                
                while (_loadQueue.TryDequeue(out var task, out var priority))
                {
                    // 只保存?Critical、High、Medium 优先级任务?
                    if (task.Priority >= LoadPriority.Low)
                    {
                        _queuedIndices.Remove(task.Index);
                        toRemove.Add(task.Index);
                    }
                    else
                    {
                        tempQueue.Enqueue(task, priority);
                    }
                }
                
                // 重新入队保留的任务?
                while (tempQueue.TryDequeue(out var task, out var priority))
                {
                    _loadQueue.Enqueue(task, priority);
                }
                
                if (toRemove.Count > 0)
                {
}
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// ?单一数据源：计算可视区域能显示多少图表?
        /// 使用 _getVisibleRangeFunc 委托获取UI实际可视范围
        /// </summary>
        private int CalculateVisibleCount()
        {
            // ?优先使用委托获取实际可视范围
            if (_getVisibleRangeFunc != null)
            {
                try
                {
                    var (first, last) = _getVisibleRangeFunc();
                    if (first >= 0 && last >= first)
                    {
                        int count = last - first + 1;
                        // ?同步更新所有配置的单源数据
                        _dynamicPolicy.SetActualVisibleSize(count);
                        _bufferConfig.SetActualVisibleSize(count);
                        return count;
                    }
                }
                catch (Exception ex)
                {
}
            }
            
            // 回退：使用当前配置值（可能过时间?
            return _dynamicPolicy.ActualVisibleSize;
        }

        /// <summary>
        /// ?P2优化：计算缓冲区域范围?
        /// ?动态范围：根据滚动类型动态调整各区域大小
        /// </summary>
        private void CalculateBufferZones(int firstVisible, int lastVisible, int totalCount)
        {
            // 活跃区域 = 当前可视范围
            _activeZone = (firstVisible, lastVisible);

            // ?动态范围：根据滚动类型动态调整预加载区域大小
            double prefetchMultiplier = _dynamicPolicy.GetPrefetchMultiplier(_currentScrollType);
            int basePrefetchSize = _bufferConfig.PrefetchSize;
            int prefetchSize = (int)(basePrefetchSize * prefetchMultiplier);
            
            // 限制最大预加载范围
            prefetchSize = Math.Min(prefetchSize, _dynamicPolicy.MaxPrefetchRange);
            _currentPrefetchSize = prefetchSize;

            // 根据滚动方向优化预加载范围?
            int leftPrefetchSize = prefetchSize;
            int rightPrefetchSize = prefetchSize;
            
            if (_scrollDirection > 0)
            {
                // 向右滚动：优先预加载右侧
                rightPrefetchSize = (int)(prefetchSize * 1.5);
                leftPrefetchSize = (int)(prefetchSize * 0.5);
            }
            else if (_scrollDirection < 0)
            {
                // 向左滚动：优先预加载左侧
                leftPrefetchSize = (int)(prefetchSize * 1.5);
                rightPrefetchSize = (int)(prefetchSize * 0.5);
            }

            // 预加载区域（左右两侧?
            _prefetchZoneLeft = (
                Math.Max(0, firstVisible - leftPrefetchSize),
                Math.Max(0, firstVisible - 1)
            );
            _prefetchZoneRight = (
                Math.Min(totalCount - 1, lastVisible + 1),
                Math.Min(totalCount - 1, lastVisible + rightPrefetchSize)
            );

            // 后备区域（预加载区域外）- 仅在静止时激?
            int reserveSize = _currentScrollType == ScrollType.Stopped ? _bufferConfig.ReserveSize : 0;
            _reserveZoneLeft = (
                Math.Max(0, _prefetchZoneLeft.start - reserveSize),
                Math.Max(0, _prefetchZoneLeft.start - 1)
            );
            _reserveZoneRight = (
                Math.Min(totalCount - 1, _prefetchZoneRight.end + 1),
                Math.Min(totalCount - 1, _prefetchZoneRight.end + reserveSize)
            );
        }

        /// <summary>
        /// ?P2优化：获取索引所属的缓冲区域
        /// </summary>
        private BufferZone GetBufferZone(int index)
        {
            // 活跃区域
            if (index >= _activeZone.start && index <= _activeZone.end)
                return BufferZone.Active;

            // 预加载区域?
            if ((index >= _prefetchZoneLeft.start && index <= _prefetchZoneLeft.end) ||
                (index >= _prefetchZoneRight.start && index <= _prefetchZoneRight.end))
                return BufferZone.Prefetch;

            // 后备区域
            if ((index >= _reserveZoneLeft.start && index <= _reserveZoneLeft.end) ||
                (index >= _reserveZoneRight.start && index <= _reserveZoneRight.end))
                return BufferZone.Reserve;

            // 超出所有缓冲区域?
            return BufferZone.Reserve;
        }

        /// <summary>
        /// ?P2优化：根据缓冲区域获取加载优先级
        /// </summary>
        private LoadPriority GetPriorityForBufferZone(BufferZone zone, int index, int firstVisible)
        {
            return zone switch
            {
                BufferZone.Active => index == firstVisible ? LoadPriority.Critical : LoadPriority.High,
                BufferZone.Prefetch => LoadPriority.Medium,
                BufferZone.Reserve => LoadPriority.Idle, // ?P3空闲优先?
                _ => LoadPriority.Low
            };
        }

        /// <summary>
        /// ?P2优化：更新滚动状态并触发缓冲区域更新
        /// ?动态范围：根据滚动类型动态调整预加载范围
        /// </summary>
        private void UpdateScrollState(int firstVisible, int lastVisible, int totalCount)
        {
            var now = DateTime.Now;

            // 计算滚动方向
            if (_lastFirstVisible >= 0)
            {
                int indexDelta = firstVisible - _lastFirstVisible;
                int previousDirection = _scrollDirection;
                _scrollDirection = indexDelta > 0 ? 1 : (indexDelta < 0 ? -1 : 0);

                // 计算滚动速度（项/秒）
                if (_lastScrollTime != DateTime.MinValue)
                {
                    var timeDelta = (now - _lastScrollTime).TotalSeconds;
                    if (timeDelta > 0)
                    {
                        _scrollVelocity = Math.Abs(indexDelta) / timeDelta;
                    }
                }

                // ?动态范围：判断滚动类型
                var previousScrollType = _currentScrollType;
                _currentScrollType = DetermineScrollType(_scrollVelocity);
                
                // 判断是否快速滚动?
                bool wasFastScrolling = _isFastScrolling;
                _isFastScrolling = _scrollVelocity > _dynamicPolicy.FastScrollThreshold;

                // 滚动类型变化处理
                if (_currentScrollType != previousScrollType)
                {
                    if (_currentScrollType == ScrollType.Fast || _currentScrollType == ScrollType.UltraFast)
                    {
}
                    else if (_currentScrollType == ScrollType.Stopped && previousScrollType != ScrollType.Stopped)
                    {
}
                }

                // 快速滚动开始/停止处理
                if (_isFastScrolling && !wasFastScrolling)
                {
}
                else if (!_isFastScrolling && wasFastScrolling)
                {
                    _lastScrollStopTime = now;
StartReserveActivationTimer(totalCount);
                }

                // 静止状态。
                if (_scrollDirection == 0 && previousDirection != 0)
                {
                    _lastScrollStopTime = now;
                    StartReserveActivationTimer(totalCount);
                }
            }

            // ?首屏加载保护：不在首屏加载期间更新计数范围?
            // 防止ScrollViewer布局事件导致_lastLastVisible被扩大，从而产生计数溢?
            if (!_visibleAreaLoadStartTime.HasValue)
            {
                _lastScrollTime = now;
                _lastFirstVisible = firstVisible;
                _lastLastVisible = lastVisible;
            }

            // 计算缓冲区域
            CalculateBufferZones(firstVisible, lastVisible, totalCount);
        }

        /// <summary>
        /// ?动态范围：根据滚动速度判断滚动类型
        /// </summary>
        private ScrollType DetermineScrollType(double velocity)
        {
            if (velocity <= 0.1) return ScrollType.Stopped;
            if (velocity < _dynamicPolicy.FastScrollThreshold) return ScrollType.Slow;
            if (velocity < _dynamicPolicy.UltraFastScrollThreshold) return ScrollType.Fast;
            return ScrollType.UltraFast;
        }

        /// <summary>
        /// ?P2优化：启动后备区域激活定时器
        /// 滚动停止后延迟一段时间，再加载后备区域的图片
        /// </summary>
        private void StartReserveActivationTimer(int totalCount)
        {
            // 停止现有定时间?
            _reserveActivationTimer?.Stop();

            // 创建新定时器
            if (_reserveActivationTimer == null)
            {
                _reserveActivationTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(_bufferConfig.ReserveActivationDelay)
                };
                _reserveActivationTimer.Tick += (s, e) =>
                {
                    _reserveActivationTimer?.Stop();
                    LoadReserveZone(totalCount);
                };
            }

            _reserveActivationTimer.Interval = TimeSpan.FromMilliseconds(_bufferConfig.ReserveActivationDelay);
            _reserveActivationTimer.Start();
        }

        /// <summary>
        /// ?P2优化：加载后备区域的图片（空闲时间?
        /// </summary>
        private void LoadReserveZone(int totalCount)
        {
            if (_imageCollection == null || totalCount <= 0) return;

            int leftCount = _reserveZoneLeft.end - _reserveZoneLeft.start + 1;
            int rightCount = _reserveZoneRight.end - _reserveZoneRight.start + 1;

            if (leftCount <= 0 && rightCount <= 0) return;
// 入队后备区域图片（Idle优先级）
            // ?修复：使用实际集合数量进行边界检查
            int actualCount = _imageCollection?.Count ?? 0;
            for (int i = _reserveZoneLeft.start; i <= _reserveZoneLeft.end && i < actualCount; i++)
            {
                if (i >= 0 && i < _imageCollection.Count)
                {
                    var filePath = _imageCollection[i].FilePath;
                    EnqueueLoadTask(i, filePath, LoadPriority.Idle);
                }
            }

            for (int i = _reserveZoneRight.start; i <= _reserveZoneRight.end && i < actualCount; i++)
            {
                if (i >= 0 && i < _imageCollection.Count)
                {
                    var filePath = _imageCollection[i].FilePath;
                    EnqueueLoadTask(i, filePath, LoadPriority.Idle);
                }
            }

            StartLoading();
        }

        /// <summary>
        /// 入队加载任务
        /// </summary>
        private void EnqueueLoadTask(int index, string filePath, LoadPriority priority)
        {
            lock (_queueLock)
            {
                // ?优化：检查是否真正需要加载（Thumbnail 是否已存在）
                if (_loadedIndices.Contains(index) || _queuedIndices.Contains(index) || _decodingIndices.Contains(index))
                    return;

                var task = new LoadTask
                {
                    Index = index,
                    FilePath = filePath,
                    Priority = priority,
                    EnqueueTime = DateTime.Now
                };

                // 优先级数值越小越优先
                _loadQueue.Enqueue(task, (int)priority);
                _queuedIndices.Add(index);
            }
        }

        /// <summary>
        /// 入队加载任务（带日志返回值）
        /// ?诊断用：返回是否成功入队
        /// </summary>
        private bool EnqueueLoadTaskWithLog(int index, string filePath, LoadPriority priority)
        {
            lock (_queueLock)
            {
                // ?优化：检查是否真正需要加载（Thumbnail 是否已存在）
                if (_loadedIndices.Contains(index))
                {
                    // 检查是否真的已加载（Thumbnail不为null?
                    // ?修复：添加index >= 0检查
                    if (_imageCollection != null && index >= 0 && index < _imageCollection.Count)
                    {
                        var thumbnail = _imageCollection[index].Thumbnail;
                        if (thumbnail == null)
                        {
                            // ?状态不一致：_loadedIndices标记已加载，但实际Thumbnail为null
_loadedIndices.Remove(index);
                            _decodingIndices.Remove(index);
                            // 继续入队
                        }
                        else
                        {
                            return false; // 确实已加载，跳过
                        }
                    }
                    else
                    {
                        return false; // 已加载，跳过
                    }
                }

                // ?新增：检查是否正在解码中
                if (_decodingIndices.Contains(index))
                {
                    return false; // 正在解码，跳转?
                }

                if (_queuedIndices.Contains(index))
                    return false; // 已在队列中，跳过

                var task = new LoadTask
                {
                    Index = index,
                    FilePath = filePath,
                    Priority = priority,
                    EnqueueTime = DateTime.Now
                };

                // 优先级数值越小越优先
                _loadQueue.Enqueue(task, (int)priority);
                _queuedIndices.Add(index);
                return true;
            }
        }

        /// <summary>
        /// 启动后台加载任务
        /// </summary>
        private void StartLoading()
        {
            if (_loadTask == null || _loadTask.IsCompleted)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
                _loadTask = ProcessLoadQueueAsync(_cancellationTokenSource.Token);
            }

            // 启动UI更新定时间?
            InitializeUIUpdateTimer();
        }

        /// <summary>
        /// 处理加载队列
        /// </summary>
        private async Task ProcessLoadQueueAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    LoadTask? task = null;
                    lock (_queueLock)
                    {
                        if (_loadQueue.Count > 0)
                        {
                            _loadQueue.TryDequeue(out task, out _);
                            if (task != null)
                            {
                                _queuedIndices.Remove(task.Index);
                            }
                        }
                    }

                    if (task == null)
                    {
                        await Task.Delay(50, cancellationToken);
                        continue;
                    }

                    VisionLogger.Instance.Info(
                        $"[LoadDiag] ProcessQueue: dequeue index={task.Index} priority={task.Priority} " +
                        $"queueRemaining={_loadQueue.Count} active={_activeTaskCount}",
                        "PriorityThumbnailLoader");

                    await _loadSemaphore.WaitAsync(cancellationToken);

                    Interlocked.Increment(ref _activeTaskCount);
                    _ = Task.Run(async () =>
                    {
                        var taskSw = Stopwatch.StartNew();
                        try
                        {
                            await LoadSingleThumbnailAsync(task, cancellationToken);
                            taskSw.Stop();
                            AdjustConcurrencyIfNeeded(taskSw.ElapsedMilliseconds);
                        }
                        catch (OperationCanceledException)
                        {
                            // 正常取消，无需处理
                        }
                        catch (Exception ex)
                        {
                            VisionLogger.Instance.Warning(
                                $"[LoadDiag] DecodeTask failed index={task.Index}: {ex.Message}",
                                "PriorityThumbnailLoader");
                        }
                        finally
                        {
                            _loadSemaphore.Release();
                            Interlocked.Decrement(ref _activeTaskCount);
                        }
                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                VisionLogger.Instance.Info("[LoadDiag] ProcessQueue cancelled (normal)", "PriorityThumbnailLoader");
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Warning(
                    $"[LoadDiag] ProcessQueue fatal: {ex.Message}", "PriorityThumbnailLoader");
                lock (_queueLock)
                {
                    _decodingIndices.Clear();
                }
                throw;
            }
        }

        /// <summary>
        /// 加载单个缩略?
        /// </summary>
        private async Task LoadSingleThumbnailAsync(LoadTask task, CancellationToken cancellationToken)
        {
            // ?修复竞态条件：在方法开始时捕获集合引用，避免在异步执行过程中集合被其他线程置空
            var imageCollection = _imageCollection;
            if (imageCollection == null || task.Index < 0 || task.Index >= imageCollection.Count)
            {
return;
            }

            // ?P2优化 - Idle优先级任务：可随时取消?
            if (task.Priority == LoadPriority.Idle)
            {
                // 检查是否有更高优先级任务?
                if (HasHigherPriorityTasks())
                {
return; // 直接放弃，不重新入队
                }

                // 检查是否还在缓冲区域内
                var zone = GetBufferZone(task.Index);
                if (zone == BufferZone.Reserve)
                {
                    // 仍在后备区域，继续加?
                }
                else
                {
                    // 已移入更高级别区域，让它以更高优先级重新入队
return;
                }
            }

            // ?P0优化 - 阶段1：开始前检查?- 低优先级任务在高负载时放?
            if (task.Priority >= LoadPriority.Low && _useLowQuality)
            {
return;
            }

            var imageInfo = imageCollection[task.Index];

            // ?优化：检查是否真正需要加载（Thumbnail 是否已存在）
            lock (_queueLock)
            {
                if (_loadedIndices.Contains(task.Index))
                {
                    // 检查是否真的已加载
                    if (imageInfo.Thumbnail == null)
                    {
                        // 状态不一致：_loadedIndices标记已加载，但实际Thumbnail为null
                        // 修复状态，继续加载
                        _loadedIndices.Remove(task.Index);
                        _queuedIndices.Remove(task.Index);
                        _decodingIndices.Remove(task.Index);
                    }
                    else
                    {
                        return; // 确实已加载，跳过
                    }
                }
                
                // ?关键修改：使?_decodingIndices 标记正在解码，而不?_loadedIndices
                _decodingIndices.Add(task.Index);
            }

            if (imageInfo.Thumbnail != null)
            {
lock (_queueLock)
                {
                    _decodingIndices.Remove(task.Index);
                    _loadedIndices.Add(task.Index); // 同步状态?
                }
                return;
            }

            var sw = Stopwatch.StartNew();
            int thumbnailSize = _useLowQuality ? 40 : 60;

            try
            {
                // ?P0优化 - 阶段2：解码前再次检查优先级（Idle任务再次检查）
                if (task.Priority == LoadPriority.Idle && HasHigherPriorityTasks())
                {
lock (_queueLock)
                    {
                        _decodingIndices.Remove(task.Index);
                    }
                    return; // 不重新入队，让更高优先级任务先执行?
                }

                // Medium/Low任务检查?
                if (task.Priority >= LoadPriority.Medium && task.Priority < LoadPriority.Idle && HasHigherPriorityTasks())
                {
lock (_queueLock)
                    {
                        _decodingIndices.Remove(task.Index);
                        // 重新入队，等待下次调试?
                        _loadQueue.Enqueue(task, (int)task.Priority);
                    }
                    return;
                }

                // 后台线程加载
                bool isHighPriorityForGpu = task.Priority <= LoadPriority.High;
                var thumbnail = await Task.Run(() => LoadThumbnailOptimized(task.FilePath, thumbnailSize, isHighPriorityForGpu));
                sw.Stop();

                VisionLogger.Instance.Info(
                    $"[LoadDiag] Decode complete index={task.Index} elapsed={sw.ElapsedMilliseconds}ms " +
                    $"thumbnail={(thumbnail != null ? $"{thumbnail.Width}x{thumbnail.Height}" : "null")}",
                    "PriorityThumbnailLoader");

                if (cancellationToken.IsCancellationRequested || thumbnail == null)
                {
                    VisionLogger.Instance.Warning(
                        $"[LoadDiag] Decode skipped index={task.Index}: cancelled={cancellationToken.IsCancellationRequested} null={thumbnail == null}",
                        "PriorityThumbnailLoader");
lock (_queueLock)
                    {
                        _decodingIndices.Remove(task.Index);
                    }
                    return;
                }

                if (thumbnail.Width <= 0 || thumbnail.Height <= 0)
                {
                    VisionLogger.Instance.Warning(
                        $"[LoadDiag] Decode invalid thumbnail index={task.Index} size={thumbnail.Width}x{thumbnail.Height}",
                        "PriorityThumbnailLoader");
lock (_queueLock)
                    {
                        _decodingIndices.Remove(task.Index);
                    }
                    return;
                }

                VisionLogger.Instance.Info(
                    $"[LoadDiag] EnqueueUIUpdate index={task.Index}", "PriorityThumbnailLoader");
                _uiUpdateQueue.Enqueue(new UIUpdateRequest
                {
                    Index = task.Index,
                    Thumbnail = thumbnail,
                    ImageInfo = imageInfo,
                    FilePath = task.FilePath,
                    DecodeSuccess = true  // ?标记解码成功
                });

                // ?方案B优化：移除提前计数，改为?ProcessUIUpdates 中实际设置成功后计数
            }
            catch (Exception ex)
            {
                lock (_queueLock)
                {
                    _decodingIndices.Remove(task.Index);
                }
                // 缩略图解码失败，不影响继续处理
            }
        }

        /// <summary>
        /// ?P0优化：检查是否有更高优先级任务等?
        /// ?P2优化：支持Idle优先级检查?
        /// </summary>
        private bool HasHigherPriorityTasks()
        {
            lock (_queueLock)
            {
                // 检查队列中是否有High或Critical任务
                return _loadQueue.UnorderedItems.Any(item => item.Element.Priority <= LoadPriority.High);
            }
        }

        /// <summary>
        /// ?P2优化：检查是否有中高优先级任务（用于Idle任务判断点?
        /// </summary>
        private bool HasMediumOrHigherPriorityTasks()
        {
            lock (_queueLock)
            {
                // 检查队列中是否有Medium及以上优先级任务
                return _loadQueue.UnorderedItems.Any(item => item.Element.Priority <= LoadPriority.Medium);
            }
        }

        /// <summary>
        /// 加载缩略图（使用委托调用实际加载方法?
        /// </summary>
        private BitmapImage? LoadThumbnailOptimized(string filePath, int size, bool isHighPriority = false)
        {
            try
            {
                if (_loadThumbnailFunc != null)
                {
                    return _loadThumbnailFunc(filePath, size, isHighPriority);
                }
                return null;
            }
            catch (Exception ex)
            {
return null;
            }
        }

        /// <summary>
        /// 动态调整并发数
        /// 优化：首屏加载期间延迟秒调整，避免误降并发。
        /// </summary>
        private void AdjustConcurrencyIfNeeded(long decodeTimeMs)
        {
            lock (_decodeTimes)
            {
                _decodeTimes.Enqueue(decodeTimeMs);
                while (_decodeTimes.Count > 20)
                    _decodeTimes.Dequeue();

                if ((DateTime.Now - _lastConcurrencyAdjust).TotalMilliseconds < 500)
                    return;

                _lastConcurrencyAdjust = DateTime.Now;
                if (_decodeTimes.Count < 10) return;

                // ?新增：首屏加载期间不调整并发，避免误?
                if (_startupTime != DateTime.MinValue && 
                    (DateTime.Now - _startupTime).TotalMilliseconds < STARTUP_GRACE_PERIOD_MS)
                {
                    return; // 首屏3秒内不调整并行?
                }

                var avgTime = _decodeTimes.Average();

                if (avgTime < 50 && _dynamicConcurrency < MAX_CONCURRENCY)
                {
                    _dynamicConcurrency = Math.Min(MAX_CONCURRENCY, _dynamicConcurrency + 1);
}
                else if (avgTime > 150 && _dynamicConcurrency > MIN_CONCURRENCY)
                {
                    _dynamicConcurrency = Math.Max(MIN_CONCURRENCY, _dynamicConcurrency - 1);
}
            }
        }

        /// <summary>
        /// 等待指定索引加载完成
        /// </summary>
        private async Task WaitForIndexLoadedAsync(int index, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                lock (_queueLock)
                {
                    if (_loadedIndices.Contains(index))
                        return;
                }
                await Task.Delay(20);
            }
            // 等待超时不输出日期?
        }

        /// <summary>
        /// 清理远离可视区域的缩略图
        /// </summary>
        private void CleanupOutOfRangeThumbnails(int firstVisible, int lastVisible, int totalCount)
        {
            lock (_queueLock)
            {
                if (_loadedIndices.Count < CLEANUP_THRESHOLD)
                    return;
            }

            var cleanupStart = Math.Max(0, firstVisible - CLEANUP_KEEP_MARGIN);
            var cleanupEnd = Math.Min(totalCount - 1, lastVisible + CLEANUP_KEEP_MARGIN);

            var indicesToRemove = new List<int>();
            lock (_queueLock)
            {
                foreach (var index in _loadedIndices)
                {
                    if (index < cleanupStart || index > cleanupEnd)
                        indicesToRemove.Add(index);
                }
            }

            if (indicesToRemove.Count > 0 && _imageCollection != null)
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    foreach (var index in indicesToRemove)
                    {
                        if (index >= 0 && index < _imageCollection.Count)
                        {
                            _imageCollection[index].Thumbnail = null;
                        }

                        lock (_queueLock)
                        {
                            _loadedIndices.Remove(index);
                        }
                    }
                    // 清理不输出日期?
                }, DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// 初始化UI更新定时间?
        /// </summary>
        private void InitializeUIUpdateTimer()
        {
            if (_uiUpdateTimer == null)
            {
                _uiUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(30)
                };
                _uiUpdateTimer.Tick += ProcessUIUpdates;
                _uiUpdateTimer.Start();
            }
        }

        /// <summary>
        /// 批量处理UI更新
        /// </summary>
        private void ProcessUIUpdates(object? sender, EventArgs e)
        {
            // ?关键修复：捕获集合引用，防止委托调用后集合变为null
            var imageCollection = _imageCollection;
            
            if (_uiUpdateQueue.Count == 0 || imageCollection == null)
            {
                if (imageCollection == null && _uiUpdateQueue.Count > 0)
                {
}
                return;
            }

            var updates = new List<UIUpdateRequest>();
            while (_uiUpdateQueue.TryDequeue(out var request))
            {
                updates.Add(request);
            }

            if (updates.Count == 0) return;

            // 获取实时可视范围
            int firstVis = _lastFirstVisible;
            int lastVis = _lastLastVisible;
            if (_getVisibleRangeFunc != null)
            {
                try
                {
                    var (f, l) = _getVisibleRangeFunc();
if (f >= 0 && l >= 0)
                    {
                        firstVis = f;
                        lastVis = l;
                    }
                }
                catch
                {
                    // 忽略异常
                }
            }

            if (imageCollection == null || imageCollection.Count == 0)
            {
return;
            }

            // 统一处理所有已解码缩略图
            int updatedCount = 0;
            int skippedCount = 0;
            int refMismatchCount = 0;
            int outOfRangeCount = 0;
            foreach (var update in updates)
            {
                if (update.Index >= 0 && update.Index < imageCollection.Count)
                {
                    if (imageCollection[update.Index] == update.ImageInfo)
                    {
                        if (update.Thumbnail != null && update.Thumbnail.Width > 0 && update.Thumbnail.Height > 0)
                        {
                            update.ImageInfo.Thumbnail = update.Thumbnail;
                            _addToCacheAction?.Invoke(update.FilePath, update.Thumbnail);
                            updatedCount++;

                            lock (_queueLock)
                            {
                                _decodingIndices.Remove(update.Index);
                                _loadedIndices.Add(update.Index);
                            }

                            Interlocked.Increment(ref _loadedInVisibleArea);
                        }
                        else
                        {
                            skippedCount++;
                            lock (_queueLock) { _decodingIndices.Remove(update.Index); }
                        }
                    }
                    else
                    {
                        refMismatchCount++;
                        lock (_queueLock) { _decodingIndices.Remove(update.Index); }
                    }
                }
                else
                {
                    outOfRangeCount++;
                    if (update.Index >= 0)
                    {
}
                    lock (_queueLock) { _decodingIndices.Remove(update.Index); }
                }
            }

            VisionLogger.Instance.Info(
                $"[LoadDiag] ProcessUIUpdates: updates={updates.Count} applied={updatedCount} " +
                $"skipped={skippedCount} refMismatch={refMismatchCount} outOfRange={outOfRangeCount} " +
                $"range=[{firstVis}-{lastVis}]",
                "PriorityThumbnailLoader");

            // ?方案B优化：使用实时可视范围判断完成?
            // 检查可视区域是否加载完成（基于实时范围，而非预期数量?
            if (_visibleAreaLoadStartTime.HasValue)
            {
                // 获取当前可视范围内的实际已加载数据?
                int actualVisibleCount = 0;
                int actualLoadedCount = 0;
                
                for (int i = firstVis; i <= lastVis && i < imageCollection.Count; i++)
                {
                    actualVisibleCount++;
                    if (imageCollection[i].Thumbnail != null)
                    {
                        actualLoadedCount++;
                    }
                }
                
                // 当实际已加载数量等于可视范围数量时，报告完成
                if (actualLoadedCount >= actualVisibleCount)
                {
                    var duration = DateTime.Now - _visibleAreaLoadStartTime.Value;
                    VisibleAreaLoadingCompleted?.Invoke(actualLoadedCount, duration);
                    _visibleAreaLoadStartTime = null;
                    _loadedInVisibleArea = 0;
                }
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _loadSemaphore?.Dispose();
                _uiUpdateTimer?.Stop();
                _reserveActivationTimer?.Stop();

                lock (_queueLock)
                {
                    _loadQueue.Clear();
                    _queuedIndices.Clear();
                    _loadedIndices.Clear();
                    _decodingIndices.Clear();
                }

                _disposed = true;
            }
        }

        #endregion

        #region 内部署?

        private class UIUpdateRequest
        {
            public int Index { get; set; }
            public BitmapImage? Thumbnail { get; set; }
            public ImageInfo? ImageInfo { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public bool DecodeSuccess { get; set; }  // ?新增：标记解码是否成?
        }

        #endregion
    }
}
