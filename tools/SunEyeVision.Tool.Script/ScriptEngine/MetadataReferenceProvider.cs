using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 统一元数据引用服务 — 为 Roslyn Workspace（UI 高亮）和 CSharpCompilation（编译 Emit）提供类型元数据。
    ///
    /// 引用来源（全部来自运行时已加载程序集，不再依赖 .NET SDK RefPack）：
    ///   1. 可信平台程序集（TRUSTED_PLATFORM_ASSEMBLIES）— 由 .NET 运行时宿主设置，必然包含
    ///      System.Private.CoreLib / System.Runtime / netstandard，确保 System.Void / System.Boolean 等
    ///      预定义类型可解析（修复 CS0518 级联误报）。在任何部署形态（框架依赖 / 自包含）下均可用。
    ///   2. 宿主已加载程序集（默认 ALC + 当前插件 ALC）— Script / IVisionImage / LogInfo / halcon 等宿主 API，
    ///      由宿主统一加载，编译元数据与运行时版本完全一致，保证脚本继承的 Script 基类与运行时类型身份一致。
    ///
    /// 不再扫描 .NET SDK RefPack 目录：部署环境（无 SDK）下 RefPack 不存在，且曾错误排除
    /// System.Private.CoreLib 导致预定义类型缺失（CS0518）。元数据统一来自运行时 ALC，符合"注入 ALC"语义。
    /// </summary>
    internal static class MetadataReferenceProvider
    {
        private static readonly object _lock = new();
        private static ImmutableList<MetadataReference>? _cachedReferences;

        /// <summary>
        /// 获取脚本编译所需的全部宿主元数据引用（BCL + 宿主 API），按文件路径去重，进程内缓存一次。
        /// </summary>
        public static ImmutableList<MetadataReference> GetHostReferences()
        {
            EnsureLoaded();
            return _cachedReferences!;
        }

        /// <summary>
        /// 清除缓存，下次调用 EnsureLoaded 时重新加载。
        /// </summary>
        public static void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedReferences = null;
            }
        }

        private static void EnsureLoaded()
        {
            if (_cachedReferences != null) return;
            lock (_lock)
            {
                if (_cachedReferences != null) return;

                var sw = System.Diagnostics.Stopwatch.StartNew();

                // 按规范化路径去重，避免重复引用导致 Roslyn CS1703
                var refs = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
                int added = 0;

                // 1. 可信平台程序集（TPA）：框架 + 宿主主程序集，必然含 System.Private.CoreLib
                added += AddTrustedPlatformAssemblies(refs);

                // 2. 宿主已加载程序集（默认 ALC + 当前插件 ALC）：脚本实际依赖的宿主 API
                added += AddLoadedContextAssemblies(refs);

                _cachedReferences = refs.Values.ToImmutableList();
                sw.Stop();

                PluginLogger.Info(
                    $"[MetadataRef] 统一引用初始化完成: 总数={_cachedReferences.Count}, TPA+ALC加入={added}, 耗时={sw.ElapsedMilliseconds}ms",
                    nameof(MetadataReferenceProvider));
            }
        }

        /// <summary>
        /// 从运行时可信平台程序集（TRUSTED_PLATFORM_ASSEMBLIES）注入 BCL 引用。
        /// TPA 由 .NET 运行时宿主设置，必然包含 System.Private.CoreLib / System.Runtime / netstandard，
        /// 是预定义类型可解析的可靠来源，不依赖 .NET SDK 是否安装。
        /// </summary>
        private static int AddTrustedPlatformAssemblies(Dictionary<string, MetadataReference> refs)
        {
            var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (string.IsNullOrEmpty(tpa))
            {
                PluginLogger.Warning(
                    "[MetadataRef] TRUSTED_PLATFORM_ASSEMBLIES 为空，BCL 引用可能缺失（将导致 CS0518）",
                    nameof(MetadataReferenceProvider));
                return 0;
            }

            int added = 0;
            foreach (var raw in tpa.Split(Path.PathSeparator))
            {
                var path = raw.Trim();
                if (path.Length == 0) continue;
                if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(path)) continue;
                if (TryAddRef(refs, path)) added++;
            }
            return added;
        }

        /// <summary>
        /// 注入宿主已加载程序集（默认 ALC + 当前插件 ALC），提供脚本实际依赖的宿主 API
        /// （Script 基类 / Plugin.SDK / CvEngine / halcon / OpenCvSharp 等）。
        /// 当前插件 ALC 确保脚本继承的 Script 基类类型身份与运行时一致。
        /// </summary>
        private static int AddLoadedContextAssemblies(Dictionary<string, MetadataReference> refs)
        {
            var seen = new HashSet<Assembly>();
            int added = 0;
            foreach (var alc in EnumerateRelevantContexts())
            {
                foreach (var asm in alc.Assemblies)
                {
                    if (!seen.Add(asm)) continue;
                    if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location)) continue;
                    if (TryAddRef(refs, asm.Location)) added++;
                }
            }
            return added;
        }

        private static IEnumerable<AssemblyLoadContext> EnumerateRelevantContexts()
        {
            yield return AssemblyLoadContext.Default;
            var current = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
            if (current != null && current != AssemblyLoadContext.Default)
                yield return current;
        }

        /// <summary>
        /// 校验为托管程序集后加入引用字典（按规范化路径去重）。
        /// 使用 AssemblyName.GetAssemblyName 既能排除原生 C++ DLL，也能排除无有效 .NET 元数据的文件，
        /// 避免 Roslyn 在编译期因无法打开原生 DLL 元数据而报 CS0006。
        /// </summary>
        private static bool TryAddRef(Dictionary<string, MetadataReference> refs, string path)
        {
            if (!IsManagedAssembly(path)) return false;

            string key;
            try { key = Path.GetFullPath(path); }
            catch { key = path; }
            if (refs.ContainsKey(key)) return false;

            try
            {
                refs[key] = MetadataReference.CreateFromFile(path);
                return true;
            }
            catch (Exception ex)
            {
                PluginLogger.Warning(
                    $"[MetadataRef] 加载引用失败: {path}, 错误: {ex.Message}",
                    nameof(MetadataReferenceProvider));
                return false;
            }
        }

        /// <summary>
        /// 判断 DLL 是否为有效托管程序集（排除原生 C++ DLL 等无 .NET 元数据的文件）。
        /// </summary>
        private static bool IsManagedAssembly(string filePath)
        {
            try
            {
                AssemblyName.GetAssemblyName(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
