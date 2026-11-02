using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
        private static readonly object AdmissionGate=new();
        private static TaskCompletionSource? AdmissionTransition;
        private readonly Guid _ownerId=Guid.NewGuid();
        private Guid? _ownedGeneration;
        private long _attempt;
        private int _lastRejectedCount;
        private readonly List<string> _admissionFailures=new();
        public string? LastAdmissionError {get;private set;}
        public RuntimeDirectorySnapshot? RuntimeDirectory => ToolRegistry.CurrentAdmission?.Runtime;
        public DesktopToolAdmission? DesktopAdmission
        {
            get { var current=ToolRegistry.CurrentAdmission; return current is null?null:new DesktopToolAdmission(current.Directory,current.Runtime); }
        }
        public IReadOnlyList<string> AdmissionFailures=>_admissionFailures.ToArray();

        public void AssertCompleteReleaseToolSet(IEnumerable<string> actualToolIds)
        {
            var actual=actualToolIds.OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            lock(AdmissionGate)
            {
            var admission=ToolRegistry.CurrentAdmission;
            if(admission is null||LastAdmissionError is not null||_lastRejectedCount!=0)
                throw new InvalidDataException("Release plugin admission failed: "+(LastAdmissionError??string.Join("; ",_admissionFailures)));
            var expected=admission.Authority.ExpectedTools.Keys.OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            if(!actual.SequenceEqual(expected,StringComparer.Ordinal))throw new InvalidDataException("Registered toolbox differs from the complete declared release ToolId set.");
            }
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
        public async Task LoadPluginsAsync()
        {
            long attempt=0, expectedGeneration=0;
            while(true)
            {
                Task? pending;
                lock(AdmissionGate)
                {
                    pending=AdmissionTransition?.Task;
                    if(pending is null){attempt=++_attempt;expectedGeneration=ToolRegistry.CurrentGeneration;break;}
                }
                await pending.ConfigureAwait(false);
            }
            string? failure=null;
            int count=0;
            TaskCompletionSource? transition=null;
            Task signal=Task.CompletedTask;
            try
            {
                // Plugin constructors, schema callbacks and file IO must remain outside the gate.
                var authority=new ReleasePayloadAuthority(AppContext.BaseDirectory);
                var snapshot=authority.PrepareDesktopDirectory();
                var components=snapshot.Components;
                var registration=_toolCatalog.PrepareRegistration(Array.Empty<Type>(),components);
                var declarations=components.Where(c=>c.Metadata.Role==SunEyeVision.Plugin.Contracts.ToolComponentRole.Runtime).Select(c=>
                {
                    var catalog=((SunEyeVision.Plugin.Contracts.IToolCatalogProvider)Activator.CreateInstance(c.EntryType)!).Catalog;
                    return new DirectoryTool(c.Metadata.ToolId,catalog.DisplayName,catalog.Category,catalog.Order,
                        catalog.IconKey,catalog.Description,c.EntryType.FullName!,c.EntryType.Assembly.Location,true);
                }).ToArray();
                var directory=new ToolDirectoryCatalog(Path.Combine(AppContext.BaseDirectory,"ToolGroups.xml"),declarations,authority.VerifyApprovedFile);
                var admission=new ToolRegistry.PublishedToolAdmission(directory,authority,snapshot,_ownerId,EnsureToolLoaded);
                RuntimeDirectorySnapshot? retiring;
                Task drained;
                lock(AdmissionGate)
                {
                    if(AdmissionTransition is not null || attempt!=_attempt || expectedGeneration!=ToolRegistry.CurrentGeneration)
                        throw new InvalidOperationException("Admission attempt was superseded by another load or unload.");
                    // Validate commit scopes before making the prior generation unavailable.
                    using(var validation=registration.EnterCommit()) { }
                    transition=new(TaskCreationOptions.RunContinuationsAsynchronously);
                    AdmissionTransition=transition;
                    retiring=ToolRegistry.CurrentAdmission?.Runtime;
                    drained=retiring?.BeginRetirement()??Task.CompletedTask;
                }
                if(retiring is not null)signal=Task.Run(retiring.SignalRetirement);
                await drained.ConfigureAwait(false);
                lock(AdmissionGate)
                {
                    if(!ReferenceEquals(AdmissionTransition,transition) || attempt!=_attempt || expectedGeneration!=ToolRegistry.CurrentGeneration)
                        throw new InvalidOperationException("Admission transition was superseded before publication.");
                    using var commit=registration.EnterCommit();
                    commit.Publish();
                    ToolRegistry.ConfigureDirectory(admission);
                    _ownedGeneration=snapshot.Generation;
                    LastAdmissionError=null;_lastRejectedCount=0;_admissionFailures.Clear();
                    count=directory.ToolIds.Count;
                    AdmissionTransition=null;transition.TrySetResult();transition=null;
                }
                await signal.ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                failure=ex.GetType().Name+": "+ex.Message;
                lock(AdmissionGate)
                {
                    if(ReferenceEquals(AdmissionTransition,transition) && transition is not null)
                    {AdmissionTransition=null;transition.TrySetResult();transition=null;}
                    if(attempt==_attempt)LastAdmissionError=failure;
                }
            }
            if(failure is null)_logger.LogInfo($"产品组件准入完成：{count}个工具。");
            else _logger.LogError("[发布准入] "+failure);
        }
        private void EnsureToolLoaded(string toolId)
        {
            var current=ToolRegistry.CurrentAdmission;
            if (current?.Directory.Find(toolId) is not null && !current.Runtime.Contains(toolId))
                throw new InvalidOperationException("产品目录缺少已准入Runtime组件：" + toolId);
        }

        /// <summary>
        /// 卸载所有插件
        /// </summary>
        public async Task UnloadPluginsAsync()
        {
            _logger.LogInfo("开始卸载插件...");
            TaskCompletionSource? transition=null;
            RuntimeDirectorySnapshot? retiring=null;
            Task drained=Task.CompletedTask;
            while(true)
            {
                Task? pending;
                lock(AdmissionGate)
                {
                    pending=AdmissionTransition?.Task;
                    if(pending is null)
                    {
                        ++_attempt;
                        var current=ToolRegistry.CurrentAdmission;
                        if(current is not null && current.Owner==_ownerId && current.Runtime.Generation==_ownedGeneration)
                        {
                            transition=new(TaskCreationOptions.RunContinuationsAsynchronously);
                            AdmissionTransition=transition;
                            retiring=current.Runtime;
                            drained=retiring.BeginRetirement();
                        }
                        else _ownedGeneration=null;
                        break;
                    }
                }
                await pending.ConfigureAwait(false);
            }
            if(transition is null){_logger.LogInfo("插件卸载完成");return;}
            Task signal=Task.CompletedTask;
            try
            {
                if(retiring is not null)signal=Task.Run(retiring.SignalRetirement);
                await drained.ConfigureAwait(false);
                lock(AdmissionGate)
                {
                    var current=ToolRegistry.CurrentAdmission;
                    if(!ReferenceEquals(AdmissionTransition,transition)||current is null || current.Owner!=_ownerId || current.Runtime.Generation!=_ownedGeneration)
                        throw new InvalidOperationException("卸载代次在排空期间发生变化。");
                    _plugins.Clear();_toolCatalog.Clear();_ownedGeneration=null;
                    AdmissionTransition=null;transition.TrySetResult();transition=null;
                }
                await signal.ConfigureAwait(false);
            }
            catch
            {
                lock(AdmissionGate)
                    if(transition is not null && ReferenceEquals(AdmissionTransition,transition))
                    {AdmissionTransition=null;transition.TrySetResult();}
                throw;
            }
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
