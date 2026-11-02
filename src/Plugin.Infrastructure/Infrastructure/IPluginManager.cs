using System.Collections.Generic;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK;

namespace SunEyeVision.Plugin.Infrastructure.Infrastructure
{
    /// <summary>
    /// 插件管理器接口 - 提供插件加载和管理功能
    /// </summary>
    public interface IPluginManager
    {
        /// <summary>
        /// 加载所有插件
        /// </summary>
        Task LoadPluginsAsync();

        /// <summary>
        /// 卸载所有插件
        /// </summary>
        Task UnloadPluginsAsync();

        /// <summary>
        /// 获取所有插件
        /// </summary>
        /// <typeparam name="T">插件类型</typeparam>
        /// <returns>插件列表</returns>
        List<T> GetPlugins<T>() where T : class;

        /// <summary>
        /// 注册插件
        /// </summary>
        /// <param name="plugin">插件实例</param>
        void RegisterPlugin(object plugin);

        /// <summary>
        /// 注销插件
        /// </summary>
        /// <param name="plugin">插件实例</param>
        void UnregisterPlugin(object plugin);

        /// <summary>
        /// 检查插件是否已加载
        /// </summary>
        /// <typeparam name="T">插件类型</typeparam>
        /// <returns>是否已加载</returns>
        bool IsPluginLoaded<T>() where T : class;
    }
}
