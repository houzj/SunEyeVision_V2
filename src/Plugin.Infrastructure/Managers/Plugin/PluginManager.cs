using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin
{
    /// <summary>
    /// 插件管理器实现
    /// </summary>
    /// <remarks>
    /// 简化后的设计：
    /// - 扫描带有 [Tool] 特性的类
    /// - 直接注册到 ToolRegistry
    /// - 不再需要中间的 Plugin 包装类
    /// </remarks>
    public class PluginManager : IPluginManager
    {
        private readonly ILogger _logger;
        private readonly Dictionary<Type, List<object>> _plugins = new();

        public PluginManager(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 无参构造函数 - 使用空日志器
        /// </summary>
        public PluginManager()
        {
            _logger = new NullLogger();
        }

        /// <summary>
        /// 加载所有插件
        /// </summary>
        public void LoadPlugins()
        {
            _logger.LogInfo("开始加载插件...");
            // 插件加载逻辑由 PluginLoader 处理
            _logger.LogInfo("插件加载完成");
        }

        /// <summary>
        /// 从指定目录加载所有插件
        /// </summary>
        /// <param name="pluginDirectory">插件目录路径</param>
        public void LoadPlugins(string pluginDirectory)
        {
            _logger.LogInfo($"开始从目录加载插件: {pluginDirectory}");
            
            if (!Directory.Exists(pluginDirectory))
            {
                _logger.LogInfo($"插件目录不存在: {pluginDirectory}");
                return;
            }

            // 只扫描 plugins 目录根层，排除 runtimes 等原生依赖目录
            var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                .Concat(
                    Directory.GetDirectories(pluginDirectory)
                        .Where(dir => !Path.GetFileName(dir).Equals("runtimes", StringComparison.OrdinalIgnoreCase)
                                   && !Path.GetFileName(dir).Equals("native", StringComparison.OrdinalIgnoreCase))
                        .SelectMany(subDir => Directory.GetFiles(subDir, "*.dll", SearchOption.AllDirectories))
                )
                .ToArray();
            _logger.LogInfo($"找到 {dllFiles.Length} 个DLL文件");

            foreach (var dllFile in dllFiles)
            {
                LoadPlugin(dllFile);
            }

            _logger.LogInfo($"插件加载完成，共加载 {ToolRegistry.GetToolCount()} 个工具");
        }

        /// <summary>
        /// 加载单个插件DLL
        /// </summary>
        private void LoadPlugin(string dllPath)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                
                // 扫描带有 [Tool] 特性的类
                var toolTypes = assembly.GetTypes()
                    .Where(t => t.GetCustomAttribute<ToolAttribute>() != null && !t.IsAbstract);

                foreach (var toolType in toolTypes)
                {
                    try
                    {
                        // 确保实现了 IToolPlugin 接口
                        if (!typeof(IToolPlugin).IsAssignableFrom(toolType))
                        {
                            _logger.LogWarning($"类型 {toolType.Name} 有 [Tool] 特性但未实现 IToolPlugin 接口");
                            continue;
                        }

                        ToolRegistry.RegisterTool(toolType);
                        _logger.LogInfo($"已加载工具: {toolType.Name}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"加载工具失败: {toolType.Name} - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"加载插件DLL失败: {dllPath} - {ex.Message}");
            }
        }

        /// <summary>
        /// 卸载所有插件
        /// </summary>
        public void UnloadPlugins()
        {
            _logger.LogInfo("开始卸载插件...");
            _plugins.Clear();
            ToolRegistry.ClearAll();
            _logger.LogInfo("插件卸载完成");
        }

        /// <summary>
        /// 获取所有插件
        /// </summary>
        /// <typeparam name="T">插件类型</typeparam>
        /// <returns>插件列表</returns>
        public List<T> GetPlugins<T>() where T : class
        {
            var pluginType = typeof(T);
            if (_plugins.TryGetValue(pluginType, out var pluginList))
            {
                return pluginList.OfType<T>().ToList();
            }
            return new List<T>();
        }

        /// <summary>
        /// 注册插件
        /// </summary>
        /// <param name="plugin">插件实例</param>
        public void RegisterPlugin(object plugin)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            var pluginType = plugin.GetType();
            var interfaces = pluginType.GetInterfaces();

            foreach (var iface in interfaces)
            {
                if (!_plugins.ContainsKey(iface))
                {
                    _plugins[iface] = new List<object>();
                }

                _plugins[iface].Add(plugin);
                _logger.LogInfo($"已注册插件: {plugin.GetType().Name} 实现接口: {iface.Name}");
            }
        }

        /// <summary>
        /// 注销插件
        /// </summary>
        /// <param name="plugin">插件实例</param>
        public void UnregisterPlugin(object plugin)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            var pluginType = plugin.GetType();
            var interfaces = pluginType.GetInterfaces();

            foreach (var iface in interfaces)
            {
                if (_plugins.TryGetValue(iface, out var pluginList))
                {
                    pluginList.Remove(plugin);
                    _logger.LogInfo($"已注销插件: {plugin.GetType().Name}");
                }
            }
        }

        /// <summary>
        /// 检查插件是否已加载
        /// </summary>
        /// <typeparam name="T">插件类型</typeparam>
        /// <returns>是否已加载</returns>
        public bool IsPluginLoaded<T>() where T : class
        {
            var pluginType = typeof(T);
            return _plugins.ContainsKey(pluginType) && _plugins[pluginType].Count > 0;
        }
    }

    /// <summary>
    /// 空日志器 - 不输出任何日志
    /// </summary>
    internal class NullLogger : ILogger
    {
        public void Log(LogLevel level, string message, string? source = null, Exception? exception = null, LogTarget target = LogTarget.UI) { }
        
        public ILogger ForSource(string source) => this;
    }
}
