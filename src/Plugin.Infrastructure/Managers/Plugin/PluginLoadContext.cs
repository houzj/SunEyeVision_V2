using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin
{
    /// <summary>
    /// 单个工具插件的「隔间」—— 独立 <see cref="AssemblyLoadContext"/> + 依赖解析器。
    /// </summary>
    /// <remarks>
    /// <b>为什么需要隔间</b>（方案《工具插件自包含与独立开发方案》§六 6.2）：
    /// <list type="bullet">
    /// <item><b>契约 / 框架模块 / 大件平台 → 回宿主</b>（<c>Load</c> 返回 <c>null</c> 即回落默认上下文）：
    ///   保证全平台**类型身份唯一**；</item>
    /// <item><b>工具自带依赖 → 隔间内解析</b>：先查依赖清单（<c>.deps.json</c>），再回落插件目录同名 dll。
    ///   这使「加一个第三方库」只改工具自己的 csproj，宿主零改动（R1/R5）。</item>
    /// </list>
    ///
    /// <b>★ 共享优先的判定顺序（事故驱动，2026-09-14）</b>：判定按"先在宿主找、再在隔间找"。
    /// 前三条都是**事实判定**，不依赖人工维护的名单，因而不会与构建期清单分叉：
    /// <list type="number">
    /// <item>契约与 <c>SunEyeVision.*</c>（类型身份硬要求）；</item>
    /// <item>框架/BCL 程序集（<c>System.*</c>…）；</item>
    /// <item>宿主**已加载**的同名程序集；</item>
    /// <item>宿主**运行目录可解析**的同名程序集。</item>
    /// </list>
    /// 只有全都不命中，才在插件目录内解析。这样「自带行李」= **设计期/独立开发期**的完整依赖，
    /// **运行期以宿主为准**——重叠即共享，杜绝缘自两份副本的**类型身份分裂**。
    ///
    /// <b>为什么原来会出错</b>：旧顺序"优先插件目录"，于是 Script 工具目录里自带的那份
    /// <c>System.Text.Json.dll</c> 被隔间优先载入 ⇒ 工具参数类上 <c>[JsonDerivedType]</c> 特性的
    /// 类型来自隔间那份 ⇒ 宿主扫描特性时"看不见" ⇒ 判别符未注册 ⇒
    /// 加载解决方案报 <c>Read unrecognized type discriminator id 'Script'</c>。
    ///
    /// <b>WPF 桥接（必需）</b>：插件界面的 BAML 由 <c>Application.LoadComponent</c> 加载，
    /// 它用 <c>Assembly.Load(名字)</c> 向**默认上下文**要程序集；插件在隔间里，默认上下文按宿主目录
    /// 探测必然找不到。故构造时登记隔间，并挂一次 <see cref="AssemblyLoadContext.Default"/>.Resolving
    /// 把请求重定向回对应隔间。
    ///
    /// <b>不 collectible 的理由</b>：插件生命周期 = 宿主进程生命周期（启动时加载一次），无卸载语义；
    /// 非 collectible 可规避「未释放引用导致卸载失败」这类隐蔽问题（YAGNI）。
    /// </remarks>
    internal sealed class PluginLoadContext : AssemblyLoadContext
    {
        /// <summary>隔间注册表：程序集简单名 → 隔间（供默认上下文重定向使用）。</summary>
        private static readonly Dictionary<string, PluginLoadContext> RegisteredContexts =
            new Dictionary<string, PluginLoadContext>(StringComparer.Ordinal);

        private static readonly object RegistryLock = new object();
        private static bool _defaultResolvingHooked;

        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _pluginDirectory;
        private readonly string _mainAssemblySimpleName;
        private readonly List<string> _locallyResolvedDependencies = new List<string>();

        /// <param name="mainAssemblyPath">插件主程序集绝对路径（<c>plugins/&lt;Name&gt;/&lt;Name&gt;.dll</c>）</param>
        internal PluginLoadContext(string mainAssemblyPath)
            : base($"Plugin:{Path.GetFileNameWithoutExtension(mainAssemblyPath)}", isCollectible: false)
        {
            var fullPath = Path.GetFullPath(mainAssemblyPath);
            _resolver = new AssemblyDependencyResolver(fullPath);
            _pluginDirectory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            _mainAssemblySimpleName = Path.GetFileNameWithoutExtension(fullPath);

            Register(this);
        }

        /// <summary>插件所在目录（自带依赖的解析基准）。</summary>
        internal string PluginDirectory => _pluginDirectory;

        /// <summary>插件主程序集简单名。</summary>
        internal string MainAssemblySimpleName => _mainAssemblySimpleName;

        /// <summary>
        /// 真正在隔间内解析的**依赖**程序集名（不含主程序集）。
        /// </summary>
        /// <remarks>
        /// 用于可诊断输出：把"目录里有几个 dll"与"实际用了几个"分开记 —— 否则
        /// "工具自带了某库、却没被使用（被宿主共享）"这类情况完全不可见（正是本次事故的盲区）。
        /// </remarks>
        internal IReadOnlyList<string> LocallyResolvedDependencies => _locallyResolvedDependencies;

        /// <summary>登记隔间，并（只做一次）挂载默认上下文的重定向桥。</summary>
        private static void Register(PluginLoadContext context)
        {
            lock (RegistryLock)
            {
                RegisteredContexts[context._mainAssemblySimpleName] = context;

                if (_defaultResolvingHooked)
                    return;

                AssemblyLoadContext.Default.Resolving += OnDefaultContextResolving;
                _defaultResolvingHooked = true;
            }
        }

        /// <summary>
        /// 默认上下文解析失败时的重定向：若请求的是已登记的插件程序集，就交给它的隔间加载。
        /// </summary>
        private static Assembly? OnDefaultContextResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            var simpleName = assemblyName.Name;
            if (string.IsNullOrEmpty(simpleName))
                return null;

            PluginLoadContext? pluginContext;
            lock (RegistryLock)
            {
                RegisteredContexts.TryGetValue(simpleName, out pluginContext);
            }

            return pluginContext?.LoadFromAssemblyName(assemblyName);
        }

        /// <summary>该程序集是否已由**宿主默认上下文**加载（已加载 ⇒ 必须共用，避免类型身份分裂）。</summary>
        private static bool IsLoadedInHost(string simpleName)
            => Default.Assemblies.Any(a => string.Equals(a.GetName().Name, simpleName, StringComparison.Ordinal));

        /// <summary>该程序集是否可**从宿主运行目录**解析到（含大件平台：构建期已剔除到宿主根）。</summary>
        private static bool IsResolvableFromHostDirectory(string simpleName)
            => File.Exists(Path.Combine(AppContext.BaseDirectory, simpleName + ".dll"));

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var simpleName = assemblyName.Name;
            if (string.IsNullOrEmpty(simpleName))
                return null;

            // ① 插件主程序集：只认本插件目录内那一份 —— **绝不回落宿主**，
            //    避免 dev/Debug 根的历史残留同名 dll 被误用（方案 §十 清理清单第 5 条）。
            if (string.Equals(simpleName, _mainAssemblySimpleName, StringComparison.Ordinal))
            {
                var selfPath = Path.Combine(_pluginDirectory, _mainAssemblySimpleName + ".dll");
                return File.Exists(selfPath) ? LoadFromAssemblyPath(selfPath) : null;
            }

            // ② 契约与框架模块（SunEyeVision.*）→ 交回宿主，类型身份唯一
            if (PluginAssemblyPolicy.IsHostProvided(simpleName))
                return null;

            // ③ 宿主**已加载**的同名程序集 → 共用（跨边界类型必须同源）
            if (IsLoadedInHost(simpleName))
                return null;

            // ④ 宿主**运行目录可解析**的同名程序集 → 共用。
            //    这是"共享优先"的兜底事实判定：与构建期行为天然一致（构建期剔除的正是宿主已有的那批），
            //    故加载期**不需要**再维护第二份平台清单（消除构建期/加载期清单分叉的可能）。
            if (IsResolvableFromHostDirectory(simpleName))
                return null;

            // ⑤ 工具自带依赖：依赖清单优先，其次插件目录同名 dll
            var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (!string.IsNullOrEmpty(resolvedPath))
                return LoadLocalDependency(resolvedPath, simpleName);

            var localPath = Path.Combine(_pluginDirectory, simpleName + ".dll");
            return File.Exists(localPath) ? LoadLocalDependency(localPath, simpleName) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            // 大件原生（halcon*/opencv*）由宿主提供：宿主运行目录已部署其原生库
            //（见 Directory.Build.targets 的 CopyHalconNativeRuntimes）⇒ 同样按"事实判定"避开本地副本。
            if (!PluginAssemblyPolicy.IsHostProvided(unmanagedDllName)
                && !IsResolvableFromHostDirectory(unmanagedDllName))
            {
                var nativePath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
                if (!string.IsNullOrEmpty(nativePath) && File.Exists(nativePath))
                    return LoadUnmanagedDllFromPath(nativePath);
            }

            // 交回默认探测（宿主运行目录）—— 不静默吞掉失败，最终由调用方抛出可读异常
            return IntPtr.Zero;
        }

        /// <summary>在隔间内载入依赖并登记（供可诊断输出）。</summary>
        private Assembly LoadLocalDependency(string path, string simpleName)
        {
            _locallyResolvedDependencies.Add(simpleName);
            return LoadFromAssemblyPath(path);
        }
    }
}
