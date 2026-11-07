namespace SunEyeVision.Foundation.Logging
{
    /// <summary>
    /// 日志来源常量 - 规范化日志来源命名
    /// </summary>
    /// <remarks>
    /// 使用此常量类确保日志来源命名一致性，便于日志过滤和问题定位。
    /// 命名规范：
    /// - 系统组件：简洁描述性名称（如"系统"、"工作流引擎"）
    /// - UI组件：组件名称（如"主窗口"、"属性面板"）
    /// - 节点：直接使用节点名称作为来源
    /// </remarks>
    public static class LogSources
    {
        /// <summary>
        /// 系统核心
        /// </summary>
        public const string System = "系统";

        /// <summary>
        /// 工作流引擎
        /// </summary>
        public const string WorkflowEngine = "工作流引擎";

        /// <summary>
        /// 图像队列
        /// </summary>
        public const string ImageQueue = "图像队列";

        /// <summary>
        /// 插件管理器
        /// </summary>
        public const string PluginManager = "插件管理器";

        /// <summary>
        /// 设备管理器
        /// </summary>
        public const string DeviceManager = "设备管理器";

        /// <summary>
        /// 主窗口
        /// </summary>
        public const string MainWindow = "主窗口";

        /// <summary>
        /// 属性面板
        /// </summary>
        public const string PropertyPanel = "属性面板";

        /// <summary>
        /// 工具箱
        /// </summary>
        public const string Toolbox = "工具箱";

        /// <summary>
        /// 画布控件
        /// </summary>
        public const string Canvas = "画布";

        /// <summary>
        /// 节点工厂
        /// </summary>
        public const string NodeFactory = "节点工厂";

        /// <summary>
        /// 日志系统
        /// </summary>
        public const string Logging = "日志系统";

        /// <summary>
        /// 配置管理
        /// </summary>
        public const string Configuration = "配置管理";
    }
}
