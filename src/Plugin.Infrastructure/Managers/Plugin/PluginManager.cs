using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Foundation.Logging;
using SunEyeVision.Plugin.Editing.Wpf.Core;
using SunEyeVision.Plugin.Infrastructure.Infrastructure;

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
        private readonly Dictionary<Type, List<object>> _plugins = new();
        private static readonly object AdmissionGate=new();
        private sealed record PublishedAdmission(DesktopToolAdmission Desktop, ReleasePayloadAuthority Authority, Guid Owner);
        private static PublishedAdmission? _current;
        private static long _generation;
        private static TaskCompletionSource? AdmissionTransition;
        private static Guid? DesktopHostGeneration;
        private static int DesktopHostPins;
        private readonly Guid _ownerId=Guid.NewGuid();
        private Guid? _ownedGeneration;
        private long _attempt;
        private int _lastRejectedCount;
        private readonly List<string> _admissionFailures=new();
        public string? LastAdmissionError {get;private set;}
        public IReadOnlyDictionary<string,long> LastAdmissionStageMs { get; private set; } = new Dictionary<string,long>();
        public RuntimeDirectorySnapshot? RuntimeDirectory { get { lock (AdmissionGate) return AdmissionTransition is null && _current?.Desktop.Runtime.IsActive == true ? _current.Desktop.Runtime : null; } }
        public DesktopToolAdmission? DesktopAdmission
        {
            get { lock (AdmissionGate) return AdmissionTransition is null && _current?.Desktop.Runtime.IsActive == true ? _current.Desktop : null; }
        }
        internal static bool IsCurrent(DesktopToolAdmission admission)
        {
            lock (AdmissionGate)
                return AdmissionTransition is null && ReferenceEquals(_current?.Desktop, admission) && admission.Runtime.IsActive;
        }
        public IReadOnlyList<string> AdmissionFailures=>_admissionFailures.ToArray();

        /// <summary>Keep a desktop workspace on one generation until it has released its consumers.</summary>
        public IDisposable PinDesktopHost()
        {
            lock(AdmissionGate)
            {
                var current=_current;
                if(AdmissionTransition is not null || current is null || current.Owner!=_ownerId || !current.Desktop.Runtime.IsActive || current.Desktop.Runtime.Generation!=_ownedGeneration)
                    throw new InvalidOperationException("桌面工作区不能绑定已变化的插件目录。");
                if(DesktopHostGeneration is not null && DesktopHostGeneration!=current.Desktop.Runtime.Generation)
                    throw new InvalidOperationException("桌面工作区已绑定另一目录代次。");
                DesktopHostGeneration=current.Desktop.Runtime.Generation;DesktopHostPins++;
                return new DesktopHostPin(current.Desktop.Runtime.Generation);
            }
        }
        private sealed class DesktopHostPin(Guid generation):IDisposable
        {
            private bool _released;
            public void Dispose()
            {
                lock(AdmissionGate)
                {
                    if(_released)return;
                    _released=true;
                    if(DesktopHostGeneration!=generation || DesktopHostPins<=0)throw new InvalidOperationException("桌面目录租约状态不一致。");
                    if(--DesktopHostPins==0)DesktopHostGeneration=null;
                }
            }
        }

        public void AssertCompleteReleaseToolSet()
        {
            lock(AdmissionGate)
            {
            var admission=_current;
            if(admission is null||AdmissionTransition is not null||!admission.Desktop.Runtime.IsActive)
                throw new InvalidDataException("Release plugin admission failed: "+(LastAdmissionError??string.Join("; ",_admissionFailures)));
            var actual=admission.Desktop.Directory.ToolIds.OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            var expected=admission.Authority.ExpectedTools.Keys.OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            if(!actual.SequenceEqual(expected,StringComparer.Ordinal))throw new InvalidDataException("Registered toolbox differs from the complete declared release ToolId set.");
            }
        }

        public PluginManager(ILogger logger)
        {
            _logger = new AdmissionLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
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
            : this(new NullLogger())
        {
        }

        /// <summary>只加载当前宿主正式载荷声明的Runtime与可选编辑器。</summary>
        public async Task LoadPluginsAsync()
        {
            var stageClock = System.Diagnostics.Stopwatch.StartNew();
            var stages = new Dictionary<string,long>();
            long attempt=0, expectedGeneration=0;
            while(true)
            {
                Task? pending;
                lock(AdmissionGate)
                {
                    pending=AdmissionTransition?.Task;
                    if(pending is null){attempt=++_attempt;expectedGeneration=_generation;break;}
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
                stages["PayloadValidation"] = stageClock.ElapsedMilliseconds;
                stageClock.Restart();
                var snapshot=authority.PrepareDesktopDirectory();
                stages["ComponentLoading"] = stageClock.ElapsedMilliseconds;
                stageClock.Restart();
                var components=snapshot.Components;
                var declarations=components.Where(c=>c.Metadata.Role==SunEyeVision.Plugin.Contracts.ToolComponentRole.Runtime).Select(c=>
                {
                    var catalog=snapshot.GetCatalog(c.Metadata.ToolId) ?? throw new InvalidOperationException("已准入工具缺少目录声明："+c.Metadata.ToolId);
                    return new DirectoryTool(c.Metadata.ToolId,catalog.DisplayName,catalog.Category,catalog.Order,
                        catalog.IconKey,catalog.Description,c.EntryType.FullName!,c.EntryType.Assembly.Location,true);
                }).ToArray();
                var directory=new ToolDirectoryCatalog(Path.Combine(AppContext.BaseDirectory,"ToolGroups.xml"),declarations,authority.VerifyApprovedFile);
                stages["ToolDirectory"] = stageClock.ElapsedMilliseconds;
                var admission=new PublishedAdmission(new DesktopToolAdmission(directory,snapshot),authority,_ownerId);
                RuntimeDirectorySnapshot? retiring;
                Task drained;
                lock(AdmissionGate)
                {
                    if(DesktopHostPins!=0)throw new InvalidOperationException("桌面工作区仍使用当前目录，不能重载插件。");
                    if(AdmissionTransition is not null || attempt!=_attempt || expectedGeneration!=_generation)
                        throw new InvalidOperationException("Admission attempt was superseded by another load or unload.");
                    transition=new(TaskCreationOptions.RunContinuationsAsynchronously);
                    AdmissionTransition=transition;
                    retiring=_current?.Desktop.Runtime;
                    drained=retiring?.BeginRetirement()??Task.CompletedTask;
                }
                if(retiring is not null)signal=Task.Run(retiring.SignalRetirement);
                await drained.ConfigureAwait(false);
                lock(AdmissionGate)
                {
                    if(!ReferenceEquals(AdmissionTransition,transition) || attempt!=_attempt || expectedGeneration!=_generation)
                        throw new InvalidOperationException("Admission transition was superseded before publication.");
                    _current=admission;
                    ++_generation;
                    _ownedGeneration=snapshot.Generation;
                    LastAdmissionError=null;_lastRejectedCount=0;_admissionFailures.Clear();
                    count=directory.ToolIds.Count;
                    AdmissionTransition=null;transition.TrySetResult();transition=null;
                }
                await ObserveRetirementSignalAsync(signal).ConfigureAwait(false);
            }
            catch(Exception ex)
            {
                failure=ex.GetType().Name+": "+ex.Message;
                lock(AdmissionGate)
                {
                    if(ReferenceEquals(AdmissionTransition,transition) && transition is not null)
                    {
                        if (_current is { } current && !current.Desktop.Runtime.IsActive)
                        { _current=null; ++_generation; }
                        AdmissionTransition=null;transition.TrySetResult();transition=null;
                    }
                    if(attempt==_attempt)LastAdmissionError=failure;
                }
            }
            LastAdmissionStageMs = new System.Collections.ObjectModel.ReadOnlyDictionary<string,long>(stages);
            if(failure is null)_logger.LogInfo($"产品组件准入完成：{count}个工具。");
            else _logger.LogError("[发布准入] "+failure);
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
                        var current=_current;
                        if(current is not null && current.Owner==_ownerId && current.Desktop.Runtime.Generation==_ownedGeneration)
                        {
                            if(DesktopHostPins!=0)throw new InvalidOperationException("桌面工作区尚未释放插件目录。");
                            transition=new(TaskCreationOptions.RunContinuationsAsynchronously);
                            AdmissionTransition=transition;
                            retiring=current.Desktop.Runtime;
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
                    var current=_current;
                    if(!ReferenceEquals(AdmissionTransition,transition)||current is null || current.Owner!=_ownerId || current.Desktop.Runtime.Generation!=_ownedGeneration)
                        throw new InvalidOperationException("卸载代次在排空期间发生变化。");
                    _plugins.Clear();_current=null;++_generation;_ownedGeneration=null;
                    AdmissionTransition=null;transition.TrySetResult();transition=null;
                }
                await ObserveRetirementSignalAsync(signal).ConfigureAwait(false);
            }
            catch
            {
                lock(AdmissionGate)
                    if(transition is not null && ReferenceEquals(AdmissionTransition,transition))
                    { if (_current is { } current && !current.Desktop.Runtime.IsActive) { _current=null; ++_generation; } AdmissionTransition=null;transition.TrySetResult();}
                throw;
            }
            _logger.LogInfo("插件卸载完成");
        }
        private async Task ObserveRetirementSignalAsync(Task signal)
        {
            // Publication or removal has already completed. A consumer callback failure
            // must not turn an accepted generation into an apparent admission failure.
            try { await signal.ConfigureAwait(false); }
            catch(Exception error) { _logger.LogError("[发布准入] 退役通知回调失败："+error); }
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
