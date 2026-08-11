using SunEyeVision.Core.Interfaces.Plugins;
using System.Collections.Generic;
using SunEyeVision.UI.Diagnostics;

namespace SunEyeVision.UI.Diagnostics
{
    /// <summary>
    /// 插件调试控制管理器
    /// 管理插件的调试功能
    /// </summary>
    public class DebugControlManager
    {
        private readonly Dictionary<string, IDebugControlProvider> _debugControls = new Dictionary<string, IDebugControlProvider>();

        /// <summary>
        /// 注册调试控制器
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <param name="provider">调试控制提供器</param>
        public void RegisterDebugControl(string pluginId, IDebugControlProvider provider)
        {
            _debugControls[pluginId] = provider;
        }

        /// <summary>
        /// 获取调试控制器
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>调试控制提供器</returns>
        public IDebugControlProvider GetDebugControl(string pluginId)
        {
            _debugControls.TryGetValue(pluginId, out var provider);
            return provider;
        }

        /// <summary>
        /// 是否有调试控制器
        /// </summary>
        /// <param name="pluginId">插件ID</param>
        /// <returns>是否有调试控制器</returns>
        public bool HasDebugControl(string pluginId)
        {
            return _debugControls.ContainsKey(pluginId);
        }
    }
}
