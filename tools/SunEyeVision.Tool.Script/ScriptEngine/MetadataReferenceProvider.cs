using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 统一元数据引用服务 — 固定路径扫描（非 ALC），扫描 AppBase 目录中白名单程序集，
    /// 为 Roslyn Workspace（UI 高亮）、CSharpCompilation（编译 Emit）、
    /// AssemblyLoadContext（沙箱加载）提供同一份宿主程序集可见性（SSOT）。
    ///
    /// 设计决策：固定路径扫描替代 ALC 扫描，提高可观测性和可控性。
    /// .NET facade（mscorlib, System, System.Core, netstandard）由 Roslyn 自动解析，不纳入。
    /// </summary>
    internal static class MetadataReferenceProvider
    {
        private static readonly object _lock = new();
        private static ImmutableList<MetadataReference>? _cachedReferences;
        private static ImmutableHashSet<string>? _cachedPaths;
        private static ImmutableHashSet<string>? _cachedNames;

        /// <summary>
        /// 白名单前缀列表 — 只有匹配的程序集才会被纳入宿主引用。
        /// 不含 .NET facade（mscorlib/System/System.Core/netstandard），由 Roslyn 自动解析。
        /// </summary>
        private static readonly string[] _allowedPrefixes =
        [
            "SunEyeVision.",
            "halcondotnet",
            "OpenCvSharp",
            "System.Windows.Forms",
            "System.Runtime",
            "System.Private.CoreLib",
        ];

        /// <summary>
        /// 获取宿主程序集的元数据引用（缓存），用于 Roslyn Workspace 和 CSharpCompilation。
        /// </summary>
        public static ImmutableList<MetadataReference> GetHostReferences()
        {
            EnsureLoaded();
            return _cachedReferences!;
        }

        /// <summary>
        /// 获取所有宿主程序集的文件路径（绝对路径集合），供沙箱 ALC 按路径精确加载。
        /// </summary>
        public static ImmutableHashSet<string> GetHostAssemblyPaths()
        {
            EnsureLoaded();
            return _cachedPaths!;
        }

        /// <summary>
        /// 获取所有宿主程序集的简单名称，供沙箱 OnResolving 名称匹配。
        /// </summary>
        public static ImmutableHashSet<string> GetHostAssemblyNames()
        {
            EnsureLoaded();
            return _cachedNames!;
        }

        /// <summary>
        /// 强制重新扫描（仅在宿主动态加载新程序集时使用，当前版本不调用，保留 API）。
        /// </summary>
        public static void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedReferences = null;
                _cachedPaths = null;
                _cachedNames = null;
            }
        }

        private static void EnsureLoaded()
        {
            if (_cachedReferences != null) return;
            lock (_lock)
            {
                if (_cachedReferences != null) return;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;

                if (!Directory.Exists(baseDir))
                {
                    PluginLogger.Warning(
                        $"[MetadataRef] AppBase 目录不存在: {baseDir}",
                        nameof(MetadataReferenceProvider));
                    _cachedReferences = ImmutableList<MetadataReference>.Empty;
                    _cachedPaths = ImmutableHashSet<string>.Empty;
                    _cachedNames = ImmutableHashSet<string>.Empty;
                    return;
                }

                var refs = ImmutableList.CreateBuilder<MetadataReference>();
                var paths = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
                var names = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
                int totalDlls = 0, filteredCount = 0, failed = 0;

                foreach (var dllPath in Directory.EnumerateFiles(baseDir, "*.dll"))
                {
                    totalDlls++;
                    var fileName = Path.GetFileNameWithoutExtension(dllPath);
                    if (string.IsNullOrEmpty(fileName)) continue;

                    // 白名单过滤
                    if (!_allowedPrefixes.Any(prefix =>
                            fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    try
                    {
                        refs.Add(MetadataReference.CreateFromFile(dllPath));
                        paths.Add(dllPath);
                        names.Add(fileName);
                        filteredCount++;
                    }
                    catch (Exception ex)
                    {
                        PluginLogger.Warning(
                            $"[MetadataRef] 创建引用失败被跳过: Name={fileName}, Path={dllPath}, 错误={ex.Message}",
                            nameof(MetadataReferenceProvider));
                        failed++;
                    }
                }

                _cachedReferences = refs.ToImmutable();
                _cachedPaths = paths.ToImmutable();
                _cachedNames = names.ToImmutable();
                sw.Stop();

                PluginLogger.Info(
                    $"[MetadataRef] 统一引用初始化完成: 基础目录总DLL={totalDlls}, 白名单命中={filteredCount}, 失败={failed}, 耗时={sw.ElapsedMilliseconds}ms",
                    nameof(MetadataReferenceProvider));
            }
        }
    }
}
