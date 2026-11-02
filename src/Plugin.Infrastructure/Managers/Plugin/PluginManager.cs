using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.Runtime;
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
    /// - 只加载正式载荷声明的Runtime及可选Wpf组件
    /// - 产品目录、工具箱与组件声明使用同一准入集合
    /// </remarks>
    public class PluginManager : IPluginManager
    {
        private readonly ILogger _logger;
        private readonly IToolCatalog _toolCatalog;
        private readonly Dictionary<Type, List<object>> _plugins = new();
        private ReleasePayloadAuthority? _releaseAuthority;
        private int _lastRejectedCount;
        private readonly List<string> _admissionFailures=new();
        public string? LastAdmissionError {get;private set;}
        public RuntimeDirectorySnapshot? RuntimeDirectory {get;private set;}
        public IReadOnlyList<string> AdmissionFailures=>_admissionFailures.ToArray();

        public void AssertCompleteReleaseToolSet(IEnumerable<string> actualToolIds)
        {
            if(_releaseAuthority is null||LastAdmissionError is not null||_lastRejectedCount!=0)
                throw new InvalidDataException("Release plugin admission failed: "+(LastAdmissionError??string.Join("; ",_admissionFailures)));
            var actual=actualToolIds.OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            var expected=_releaseAuthority.ExpectedTools.Keys.OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            if(!actual.SequenceEqual(expected,StringComparer.Ordinal))throw new InvalidDataException("Registered toolbox differs from the complete declared release ToolId set.");
        }

        public PluginManager(ILogger logger)
            : this(logger, new ToolRegistryCatalog())
        {
        }

        internal PluginManager(ILogger logger, IToolCatalog toolCatalog)
        {
            _logger = new AdmissionLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
            _toolCatalog = toolCatalog ?? throw new ArgumentNullException(nameof(toolCatalog));
        }

        // Logging is outside publication gates and cannot turn an already-published
        // assembly into a reported rejection (or prevent rejection diagnostics).
        private sealed class AdmissionLogger : ILogger
        {
            private readonly ILogger _inner;
            internal AdmissionLogger(ILogger inner){_inner=inner;}
            public void Log(LogLevel level,string message,string? source=null,Exception? exception=null,LogTarget target=LogTarget.All)
            {try{_inner.Log(level,message,source,exception,target);}catch{System.Diagnostics.Trace.WriteLine("Plugin admission diagnostic sink failed: "+message);}}
            public ILogger ForSource(string source)=>new AdmissionLogger(_inner.ForSource(source));
        }

        /// <summary>
        /// 无参构造函数 - 使用空日志器
        /// </summary>
        public PluginManager()
            : this(new NullLogger(), new ToolRegistryCatalog())
        {
        }

        /// <summary>只加载当前宿主正式载荷声明的Runtime与可选编辑器。</summary>
        public void LoadPlugins()
        {
            LastAdmissionError=null;_lastRejectedCount=0;_releaseAuthority=null;_admissionFailures.Clear();
            try
            {
                var authority = new ReleasePayloadAuthority(AppContext.BaseDirectory);
                var snapshot = authority.PrepareDesktopDirectory();
                var components = snapshot.Components;
                var registration = _toolCatalog.PrepareRegistration(Array.Empty<Type>(), components);
                var declarations = components.Where(c => c.Metadata.Role == SunEyeVision.Plugin.Contracts.ToolComponentRole.Runtime).Select(c =>
                {
                    var catalog = ((SunEyeVision.Plugin.Contracts.IToolCatalogProvider)Activator.CreateInstance(c.EntryType)!).Catalog;
                    return new DirectoryTool(c.Metadata.ToolId, catalog.DisplayName, catalog.Category, catalog.Order,
                        catalog.IconKey, catalog.Description, c.EntryType.FullName!, c.EntryType.Assembly.Location, true);
                }).ToArray();
                var directory = new ToolDirectoryCatalog(Path.Combine(AppContext.BaseDirectory, "ToolGroups.xml"), declarations, authority.VerifyApprovedFile);
                // 分类、组件和所有校验先完成，之后才发布注册和目录。
                using (var commit = registration.EnterCommit())
                {
                    commit.Publish();
                    ToolRegistry.ConfigureDirectory(directory, EnsureToolLoaded);
                }
                _releaseAuthority = authority;
                RuntimeDirectory = snapshot;
                _logger.LogInfo($"产品组件准入完成：{directory.ToolIds.Count}个工具。");
            }
            catch (Exception ex)
            {
                LastAdmissionError = ex.GetType().Name + ": " + ex.Message;
                _logger.LogError("[发布准入] " + LastAdmissionError);
            }
        }

        private void EnsureToolLoaded(string toolId)
        {
            if (ToolRegistry.DirectoryCatalog?.Find(toolId) is not null && !ToolRegistry.HasRuntime(toolId))
                throw new InvalidOperationException("产品目录缺少已准入Runtime组件：" + toolId);
        }

        /// <summary>
        /// 卸载所有插件
        /// </summary>
        public void UnloadPlugins()
        {
            _logger.LogInfo("开始卸载插件...");
            _plugins.Clear();
            _toolCatalog.Clear();
            RuntimeDirectory=null;
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
