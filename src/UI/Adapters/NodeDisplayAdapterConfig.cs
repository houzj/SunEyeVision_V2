using System;
using System.IO;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Services.Logging;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Adapters
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
                solutionsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "solutions");
            }

            // 创建解决方案管理器
            _solutionManager = new SolutionManager(solutionsDirectory);
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
            // 1. Debug写入器 - 输出到VS调试窗口
            var debugWriter = new DebugLogWriter();
            logger.AddWriter(debugWriter);

            // 2. 文件写入器 - 持久化到文件
            var fileWriter = new FileLogWriter();
            logger.AddWriter(fileWriter);

            // 3. UI写入器 - 通过延迟初始化方法创建，确保全局共享唯一实例
            InitializeUILogWriter();

            // 设置 PluginLogger 的提供者
            Plugin.SDK.Logging.PluginLogger.SetProvider(logger);

            // 测试日志
            logger.Info("日志系统初始化完成", "ServiceInitializer");
        }
    }
}
