using System;
using System.IO;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Services.Logging;
using SunEyeVision.Workflow;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Services.Communication;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters
{
    /// <summary>
    /// 节点显示适配器配置类
    /// 用于注册和初始化所有节点显示适配器。
    /// </summary>
    public static class NodeDisplayAdapterConfig
    {
        private static bool _isInitialized = false;

    /// <summary>
    /// 初始化所有节点显示适配器。
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        // 注册图像源节点适配器。
        NodeDisplayAdapterFactory.RegisterAdapter("ImageSource", new ImageSourceNodeDisplayAdapter());

        // 注册视频源节点适配器。
        NodeDisplayAdapterFactory.RegisterAdapter("VideoSource", new VideoSourceNodeDisplayAdapter());

        // 注册多集合节点适配器。
        NodeDisplayAdapterFactory.RegisterAdapter("MultiCollection", new MultiCollectionNodeDisplayAdapter());

        // 注册处理节点适配器。
        NodeDisplayAdapterFactory.RegisterAdapter("Processing", new ProcessingNodeDisplayAdapter());

        // 注册AI分析节点适配器。
        NodeDisplayAdapterFactory.RegisterAdapter("AIAnalysis", new AIAnalysisNodeDisplayAdapter());

        // 注册控制流节点适配器（子程序、条件判断等）。
        NodeDisplayAdapterFactory.RegisterAdapter("Subroutine", new ControlFlowNodeDisplayAdapter());
        NodeDisplayAdapterFactory.RegisterAdapter("Condition", new ControlFlowNodeDisplayAdapter());

        // 可以继续添加其他类型的适配器...

        _isInitialized = true;
    }

        /// <summary>
        /// 重置适配器配置。
        /// </summary>
        public static void Reset()
        {
            NodeDisplayAdapterFactory.Clear();
            _isInitialized = false;
        }
    }

    /// <summary>
    /// 服务初始化扩展方法类。
    /// </summary>
    public static class ServiceInitializer
    {
        private static UILogWriter? _uiLogWriter;
        private static SolutionManager? _solutionManager;
        private static ApplicationDataPaths? _dataPaths;
        private static FileLogWriter? _fileLogWriter;
        public static void ConfigureDataPaths(ApplicationDataPaths paths) => _dataPaths = paths;

        public static void InitializeSolutionManager(ApplicationDataPaths paths)
        {
            if (_solutionManager != null) return;
            _dataPaths = paths;
            _solutionManager = new SolutionManager(paths);
            _ = CommunicationService;
        }
        private static CameraRuntimeComposition? _cameraRuntime;
        private static CommunicationRuntimeComposition? _communicationRuntime;
        private static CommunicationApplicationService? _communicationService;

        /// <summary>
        /// 获取 UI 日志写入器（供 LogPanelViewModel 使用）
        /// </summary>
        /// <remarks>
        /// 支持延迟自动创建：如果尚未通过 InitializeServices() 初始化，
        /// 首次访问时会自动创建实例并注册到 VisionLogger，确保任何时序访问都安全。
        /// </remarks>
        public static UILogWriter UILogWriter
        {
            get
            {
                if (_uiLogWriter == null)
                {
                    InitializeUILogWriter();
                }
                return _uiLogWriter!;
            }
        }

        /// <summary>
        /// 获取解决方案管理器
        /// </summary>
        public static SolutionManager SolutionManager => _solutionManager ?? throw new InvalidOperationException("SolutionManager 未初始化，请先调用 InitializeSolutionManager()");

        public static CameraRuntimeComposition CameraRuntime
            => _cameraRuntime ??= new CameraRuntimeComposition();

        public static CommunicationRuntimeComposition CommunicationRuntime
            => _communicationRuntime ??= new CommunicationRuntimeComposition();

        public static CommunicationApplicationService CommunicationService
        {
            get
            {
                if (_communicationService == null)
                {
                    _communicationService = new CommunicationApplicationService(SolutionManager, CommunicationRuntime);
                    _communicationService.Automation.CameraConnectionObserver = id =>
                        _cameraRuntime != null && _cameraRuntime.Runtime.TryGetSession(
                            new SunEyeVision.Plugin.SDK.Devices.Camera.CameraDeviceRef(id), out var session)
                        && session.Adapter.State is SunEyeVision.Plugin.SDK.Devices.Camera.CameraState.Connected
                            or SunEyeVision.Plugin.SDK.Devices.Camera.CameraState.Capturing;
                }
                return _communicationService;
            }
        }

        public static async ValueTask DisposeCommunicationRuntimeAsync()
        {
            if (_communicationService is not null)
            {
                await _communicationService.DisposeAsync().ConfigureAwait(false);
                _communicationService = null;
            }
            if (_communicationRuntime is null) return;
            await _communicationRuntime.DisposeAsync().ConfigureAwait(false);
            _communicationRuntime = null;
        }

        public static async ValueTask DisposeCameraRuntimeAsync()
        {
            if (_cameraRuntime is null)
                return;
            await _cameraRuntime.DisposeAsync().ConfigureAwait(false);
            _cameraRuntime = null;
        }

        /// <summary>
        /// 初始化所有服务。
        /// </summary>
        public static void InitializeServices()
        {
            // 初始化显示适配器配置。
            NodeDisplayAdapterConfig.Initialize();

            // 初始化插件日志器 - 让工具项目可以访问全局日志器
            InitializePluginLogger();
        }

        /// <summary>
        /// 初始化解决方案管理器
        /// </summary>
        /// <param name="solutionsDirectory">解决方案目录，默认为solutions/</param>
        public static void InitializeSolutionManager(string? solutionsDirectory = null)
        {
            if (_solutionManager != null)
            {
                return;
            }

            var logger = VisionLogger.Instance;
            logger.Info("正在初始化解决方案管理器...", "ServiceInitializer");

            // 如果未指定解决方案目录，使用默认目录
            if (string.IsNullOrWhiteSpace(solutionsDirectory))
            {
                solutionsDirectory = _dataPaths?.SolutionsDirectory ?? throw new InvalidOperationException("请先选择数据目录。");
            }

            // 创建解决方案管理器
            _solutionManager = new SolutionManager(solutionsDirectory);
            // Subscribe before any solution is opened, even when the manager window is never shown.
            _ = CommunicationService;
            logger.Info($"解决方案管理器初始化完成，目录: {_solutionManager.SolutionsDirectory}", "ServiceInitializer");
        }

        /// <summary>
        /// 初始化 UILogWriter（延迟自动创建入口）
        /// </summary>
        /// <remarks>
        /// 可在两种场景下被调用：
        /// 1. InitializePluginLogger() 中预先创建（运行时正常路径）
        /// 2. UILogWriter 属性首次访问时延迟创建（设计时或特殊时序）
        /// 使用锁确保线程安全，仅创建一次。
        /// </remarks>
        private static readonly object _uiLogWriterLock = new();
        private static void InitializeUILogWriter()
        {
            lock (_uiLogWriterLock)
            {
                if (_uiLogWriter != null)
                    return;

                var logger = VisionLogger.Instance;
                _uiLogWriter = new UILogWriter();
                logger.AddWriter(_uiLogWriter);
            }
        }

        /// <summary>
        /// 初始化插件日志器
        /// </summary>
        private static void InitializePluginLogger()
        {
            var logger = VisionLogger.Instance;

            // 添加默认日志写入器（确保日志从一开始就有输出）
            // 1. 文件写入器 - 持久化到本地（双轨制：UI显示 + 文件落盘）
            //    日志根目录与 docs/ 平级，位于项目根下 logs/。
            var logRoot = _dataPaths?.LogDirectory ?? Path.Combine(Path.GetTempPath(), "SunEyeVision", "startup-logs");
            if (Directory.Exists(logRoot))
            {
                var files = Directory.GetFiles(logRoot, "*.txt", SearchOption.AllDirectories).Select(path => new FileInfo(path)).OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
                long retained = 0;
                foreach (var file in files)
                {
                    retained += file.Length;
                    if (file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-30) || retained > 256L * 1024 * 1024)
                        try { file.Delete(); } catch (IOException) { }
                }
            }
            var fileWriter = new FileLogWriter(logRoot);
            logger.AddWriter(fileWriter);

            // 2. UI写入器 - 通过延迟初始化方法创建，确保全局共享唯一实例
            InitializeUILogWriter();

            // 设置 PluginLogger 的提供者
            Plugin.SDK.Logging.PluginLogger.SetProvider(logger);

            // 测试日志
            logger.Info("日志系统初始化完成", "ServiceInitializer");
        }

        /// <summary>
        /// 从程序运行目录逐级向上查找包含 docs/ 子目录的祖先目录，即项目根。
        /// 使日志根目录 logs/ 与 docs/ 平级。找不到则返回 null（由调用方兜底到运行目录）。
        /// </summary>
        private static string? ResolveProjectRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "docs")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
