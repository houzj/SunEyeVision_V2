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
        private readonly IToolCatalog _toolCatalog;
        private readonly Dictionary<Type, List<object>> _plugins = new();
        private ReleasePayloadAuthority? _releaseAuthority;
        private int _lastRejectedCount;
        private readonly List<string> _admissionFailures=new();
        public string? LastAdmissionError {get;private set;}
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

        public PluginManager(ILogger logger, IToolCatalog toolCatalog)
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
            LastAdmissionError=null;_lastRejectedCount=0;_releaseAuthority=null;_admissionFailures.Clear();
            try{_releaseAuthority=new ReleasePayloadAuthority(AppContext.BaseDirectory);}
            catch(Exception ex)
            {
                LastAdmissionError=ex.GetType().Name+": "+ex.Message;
                _logger.LogError("[发布准入] "+LastAdmissionError);
                return;
            }

            if (!Directory.Exists(pluginDirectory))
            {
                LastAdmissionError="Plugin directory does not exist: "+pluginDirectory;
                _logger.LogInfo($"插件目录不存在: {pluginDirectory}");
                return;
            }

            // 目录发现不触发工具构造、WPF组件加载或参数类型注册。
            try
            {
                var directory=new ToolDirectoryCatalog(pluginDirectory,Path.Combine(AppContext.BaseDirectory,"ToolGroups.xml"),_releaseAuthority.VerifyApprovedFile);

                ToolRegistry.ConfigureDirectory(directory,EnsureToolLoaded);
                _logger.LogInfo($"目录发现完成：{directory.Groups.Count}个分类、{directory.ToolIds.Count}个工具；组件将在使用时加载。");
            }
            catch(Exception ex) { LastAdmissionError=ex.Message; _logger.LogError("工具目录读取失败："+ex.Message); }
        }

        private readonly object _loadGate=new();
        private readonly HashSet<string> _loadedPaths=new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loadingPaths=new(StringComparer.OrdinalIgnoreCase);
        private void EnsureToolLoaded(string toolId)
        {
            lock(_loadGate)
            {
                var tool=ToolRegistry.DirectoryCatalog?.Find(toolId);
                if(tool is null)return;
                if(_loadedPaths.Contains(tool.AssemblyPath))return;
                if(!_loadingPaths.Add(tool.AssemblyPath))throw new InvalidOperationException("工具加载发生递归："+toolId);
                try
                {
                    if(LoadPlugin(tool.AssemblyPath)!=PluginLoadOutcome.Loaded)
                        throw new InvalidOperationException("工具组件创建准备失败："+toolId+"；"+_admissionFailures.LastOrDefault());
                    _loadedPaths.Add(tool.AssemblyPath);
                }
                finally { _loadingPaths.Remove(tool.AssemblyPath); }
            }
        }
        /// <summary>单个插件的加载结果（用于汇总"成功 / 未加载"计数）。</summary>
        private enum PluginLoadOutcome
        {
            /// <summary>已加载并至少注册了一个工具。</summary>
            Loaded,

            /// <summary>被拒绝（契约不兼容 / 依赖缺失 / 无工具类型）—— 只跳过它自己。</summary>
            Rejected,
        }

        /// <summary>
        /// 在**独立隔间**中加载单个插件DLL。
        /// </summary>
        /// <remarks>
        /// 关键顺序与理由：
        /// <list type="number">
        /// <item><b>先隔间、后加载</b>：契约 / 框架模块走宿主回落，保证 <c>IToolPlugin</c> 类型身份唯一；</item>
        /// <item><b>先核对证件、后扫描类型</b>：契约版本不符时给出**可自助修复**的原因；
        ///   若反过来先 <c>GetTypes()</c>，会因新版本依赖缺失而抛出"找不到程序集"的误导性异常；</item>
        /// <item><b>失败只跳过该插件</b>（Fail-Forward）：坏插件不影响其它插件与宿主启动。</item>
        /// </list>
        /// </remarks>
        private PluginLoadOutcome LoadPlugin(string dllPath)
        {
            var pluginFileName = Path.GetFileName(dllPath);

            try
            {
                var context = new PluginLoadContext(dllPath,_releaseAuthority??throw new InvalidOperationException("Release authority is unavailable."));
                var assembly = context.LoadFromAssemblyPath(dllPath);

                // 证件核对（ToolContract）：判定依据是插件程序集显式契约元数据，
                // 与 CLR AssemblyVersion 分离。
                var pluginContractVersion = ToolContract.GetPluginContractVersion(assembly);
                if (!ToolContract.IsCompatible(pluginContractVersion))
                {
                    _admissionFailures.Add(pluginFileName+": "+ToolContract.Describe(pluginContractVersion));
                    _logger.LogError($"[插件契约] 拒绝加载 {pluginFileName}：{ToolContract.Describe(pluginContractVersion)}");
                    return PluginLoadOutcome.Rejected;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // 依赖不全时把**每个**加载异常都列出来，否则只有一句"类型加载失败"，无从下手
                    var details = string.Join("; ",
                        ex.LoaderExceptions.Where(e => e != null).Select(e => e!.Message).Distinct());
                    _admissionFailures.Add(pluginFileName+": type scan failed: "+details);
                    _logger.LogError($"[插件] {pluginFileName} 类型扫描失败（依赖缺失？）: {details}");
                    return PluginLoadOutcome.Rejected;
                }

                var toolTypes = types
                    .Where(t => t.GetCustomAttribute<ToolAttribute>() != null && !t.IsAbstract)
                    .ToArray();

                if (toolTypes.Length == 0)
                {
                    _admissionFailures.Add(pluginFileName+": no declared tool types.");
                    _logger.LogWarning($"[插件] {pluginFileName} 未发现带 [Tool] 的工具类型"
                        + "（约定：主程序集须与所在目录同名，请确认拷入的是工具主程序集）");
                    return PluginLoadOutcome.Rejected;
                }

                _releaseAuthority.VerifyToolSet(dllPath,toolTypes);
                var components=_releaseAuthority.LoadToolComponents(toolTypes.Select(t=>t.GetCustomAttribute<ToolAttribute>()!.Id).ToHashSet(StringComparer.Ordinal));

                // Prepare the whole assembly before any tool, serializer or WPF bridge becomes visible.
                var registration=_toolCatalog.PrepareRegistration(toolTypes,components);
                var bridge=context.PrepareBridge();
                using(var catalogCommit=registration.EnterCommit())
                using(var bridgeCommit=bridge.EnterCommit())
                {
                    catalogCommit.Publish();
                    bridgeCommit.Publish();
                }
                foreach(var toolType in toolTypes)_logger.LogInfo($"已加载工具: {toolType.Name}");
                // 可诊断输出（本次事故的盲区）：把"目录里带了什么"与"实际在隔间里解析了什么"分开记。
                // 否则"工具自带了某库、却被宿主共享"这类情况完全不可见 —— 而这正是
                // 「[JsonDerivedType] 特性读不到 ⇒ 判别符未注册」那类问题的第一现场。
                _logger.LogInfo($"[插件] {pluginFileName}：隔间内解析依赖 {context.LocallyResolvedDependencies.Count} 个"
                    + "（其余同名副本由宿主提供/未被使用）");

                return PluginLoadOutcome.Loaded;
            }
            catch (Exception ex)
            {
                _admissionFailures.Add(pluginFileName+": "+ex.GetType().Name+": "+ex.Message);
                // 报错优先（rule-008）：绝不静默吞掉加载失败
                _logger.LogError($"[插件] 加载失败: {pluginFileName} - {ex.GetType().Name}: {ex.Message}");
                return PluginLoadOutcome.Rejected;
            }
        }

        /// <summary>
        /// 卸载所有插件
        /// </summary>
        public void UnloadPlugins()
        {
            _logger.LogInfo("开始卸载插件...");
            lock(_loadGate) { _loadedPaths.Clear(); _loadingPaths.Clear(); }
            _plugins.Clear();
            _toolCatalog.Clear();
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
