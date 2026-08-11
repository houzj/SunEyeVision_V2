namespace SunEyeVision.UI.Diagnostics
{
    /// <summary>
    /// 调试控制提供者接口。
    /// 插件可实现此接口提供自定义调试控件。
    /// </summary>
    public interface IDebugControlProvider
    {
        /// <summary>
        /// 获取调试控制面板
        /// </summary>
        /// <returns>调试控制面板</returns>
        object GetDebugPanel();

        /// <summary>
        /// 开始调试
        /// </summary>
        void StartDebug();

        /// <summary>
        /// 停止调试
        /// </summary>
        void StopDebug();

        /// <summary>
        /// 单步执行
        /// </summary>
        void Step();

        /// <summary>
        /// 重置调试
        /// </summary>
        void Reset();

        /// <summary>
        /// 检查是否有自定义调试控件
        /// </summary>
        /// <param name="toolType">工具类型</param>
        /// <returns>是否有自定义控件</returns>
        bool HasCustomDebugControl(string toolType);

        /// <summary>
        /// 创建自定义调试控件。
        /// </summary>
        /// <param name="toolType">工具类型</param>
        /// <returns>自定义控件实例。</returns>
        System.Windows.FrameworkElement CreateDebugControl(string toolType);
    }
}
