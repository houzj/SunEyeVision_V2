namespace SunEyeVision.Foundation.Logging
{
    /// <summary>
    /// 日志来源构建器 - 标准化的层级来源生成
    /// </summary>
    /// <remarks>
    /// 层级结构：一级.二级.三级
    /// - 一级：系统/运行/设备/UI
    /// - 二级：具体模块/工作流名称
    /// - 三级：具体组件/节点名称
    /// 
    /// 使用示例：
    /// <code>
    /// logger.Info("启动完成", LogSource.System("Core"));
    /// logger.Error("相机断开", LogSource.Device("相机", "工业相机1"));
    /// logger.Info("执行完成", LogSource.Runtime("检测流程", "边缘检测"));
    /// </code>
    /// </remarks>
    public static class LogSource
    {
        #region 系统层来源

        /// <summary>
        /// 系统层来源
        /// </summary>
        /// <param name="layer">层级：Core/Plugin/Device/Config</param>
        public static string System(string layer) => $"系统.{layer}";

        /// <summary>
        /// 系统层来源（带组件）
        /// </summary>
        public static string System(string layer, string component) => $"系统.{layer}.{component}";

        #endregion

        #region 运行层来源

        /// <summary>
        /// 运行层来源
        /// </summary>
        /// <param name="workflow">工作流名称</param>
        public static string Runtime(string workflow) => $"运行.{workflow}";

        /// <summary>
        /// 运行层来源（带节点）
        /// </summary>
        public static string Runtime(string workflow, string node) => $"运行.{workflow}.{node}";

        /// <summary>
        /// 运行层来源（完整三级）
        /// </summary>
        public static string Runtime(string workflow, string node, string tool) => $"运行.{workflow}.{node}.{tool}";

        #endregion

        #region 设备层来源

        /// <summary>
        /// 设备层来源
        /// </summary>
        /// <param name="deviceType">设备类型：相机/IO/串口</param>
        public static string Device(string deviceType) => $"设备.{deviceType}";

        /// <summary>
        /// 设备层来源（带设备名）
        /// </summary>
        public static string Device(string deviceType, string deviceName) => $"设备.{deviceType}.{deviceName}";

        #endregion

        #region UI层来源

        /// <summary>
        /// UI层来源
        /// </summary>
        /// <param name="action">操作类型：操作/配置/节点</param>
        public static string UI(string action) => $"UI.{action}";

        /// <summary>
        /// UI层来源（带具体操作）
        /// </summary>
        public static string UI(string action, string detail) => $"UI.{action}.{detail}";

        #endregion

        #region 常用来源常量

        // 系统层
        /// <summary>
        /// 系统核心
        /// </summary>
        public static string SystemCore => System("Core");

        /// <summary>
        /// 系统插件
        /// </summary>
        public static string SystemPlugin => System("Plugin");

        /// <summary>
        /// 系统设备
        /// </summary>
        public static string SystemDevice => System("Device");

        /// <summary>
        /// 系统配置
        /// </summary>
        public static string SystemConfig => System("Config");

        // 设备层
        /// <summary>
        /// 设备-相机
        /// </summary>
        public static string DeviceCamera => Device("相机");

        /// <summary>
        /// 设备-IO
        /// </summary>
        public static string DeviceIO => Device("IO");

        /// <summary>
        /// 设备-串口
        /// </summary>
        public static string DeviceSerial => Device("串口");

        // UI层
        /// <summary>
        /// UI节点操作
        /// </summary>
        public static string UINode => UI("节点");

        /// <summary>
        /// UI常规操作
        /// </summary>
        public static string UIOperation => UI("操作");

        /// <summary>
        /// UI配置
        /// </summary>
        public static string UIConfig => UI("配置");

        /// <summary>
        /// UI连接操作
        /// </summary>
        public static string UIConnection => UI("连接");

        /// <summary>
        /// UI选择操作
        /// </summary>
        public static string UISelection => UI("选择");

        /// <summary>
        /// UI调试
        /// </summary>
        public static string UIDebug => UI("调试");

        /// <summary>
        /// UI文件操作
        /// </summary>
        public static string UIFile => UI("文件");

        /// <summary>
        /// UI编辑操作
        /// </summary>
        public static string UIEdit => UI("编辑");

        #endregion

        #region 扩展来源方法

        /// <summary>
        /// 工作流执行来源
        /// </summary>
        public static string WorkflowExecution(string workflowName) => Runtime(workflowName);

        /// <summary>
        /// 节点执行来源
        /// </summary>
        public static string NodeExecution(string workflowName, string nodeName)
            => Runtime(workflowName, nodeName);

        /// <summary>
        /// 工具执行来源
        /// </summary>
        public static string ToolExecution(string workflowName, string nodeName, string toolName)
            => Runtime(workflowName, nodeName, toolName);

        /// <summary>
        /// 插件加载来源
        /// </summary>
        public static string PluginLoad(string pluginName) => System("Plugin", pluginName);

        /// <summary>
        /// 设备连接来源
        /// </summary>
        public static string DeviceConnect(string deviceType, string deviceName)
            => Device(deviceType, deviceName);

        #endregion
    }
}
