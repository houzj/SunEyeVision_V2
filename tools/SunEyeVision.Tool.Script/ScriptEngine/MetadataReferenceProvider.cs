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
                int totalDlls = 0, filteredCount = 0, filteredRefPack = 0, failed = 0;

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

                // 补充：从 .NET SDK Ref Pack 加载运行时核心引用（如 System.Private.CoreLib）
                // 这些程序集不在发布输出目录（dev/Debug/）中，但 Roslyn 编译需要它们来识别
                // System.Void、System.Int32 等预定义类型（修复 CS0518 级联误报）
                filteredRefPack = TryLoadRefPackFallback(refs, paths, names);

                _cachedReferences = refs.ToImmutable();
                _cachedPaths = paths.ToImmutable();
                _cachedNames = names.ToImmutable();
                sw.Stop();

                PluginLogger.Info(
                    $"[MetadataRef] 统一引用初始化完成: 基础目录总DLL={totalDlls}, 白名单命中={filteredCount}, RefPack补充={filteredRefPack}, 失败={failed}, 耗时={sw.ElapsedMilliseconds}ms",
                    nameof(MetadataReferenceProvider));
            }
        }

        /// <summary>
        /// 从 .NET SDK Ref Pack 扫描补充运行时核心引用（如 System.Private.CoreLib）。
        /// 这些程序集不在发布输出目录中，但 Roslyn 编译需要它们识别预定义类型。
        /// </summary>
        /// <returns>本次补充的引用数量</returns>
        private static int TryLoadRefPackFallback(
            ImmutableList<MetadataReference>.Builder refs,
            ImmutableHashSet<string>.Builder paths,
            ImmutableHashSet<string>.Builder names)
        {
            int count = 0;
            try
            {
                var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
                if (string.IsNullOrEmpty(dotnetRoot))
                {
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    if (string.IsNullOrEmpty(programFiles)) return count;
                    dotnetRoot = Path.Combine(programFiles, "dotnet");
                }

                var packsDir = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
                if (!Directory.Exists(packsDir)) return count;

                // 找最新版本的 Ref Pack
                Version? latestVersion = null;
                foreach (var dir in Directory.EnumerateDirectories(packsDir))
                {
                    var dirName = Path.GetFileName(dir);
                    if (Version.TryParse(dirName, out var v))
                    {
                        if (latestVersion == null || v > latestVersion)
                            latestVersion = v;
                    }
                }

                if (latestVersion == null) return count;

                // 从版本号推导 TFM 目录名：8.x → net8.0, 9.x → net9.0
                var tfm = $"net{latestVersion.Major}.0";
                var refDir = Path.Combine(packsDir, latestVersion.ToString(), "ref", tfm);
                if (!Directory.Exists(refDir))
                {
                    // 兜底：扫描 ref/ 下第一个存在的 TFM 子目录
                    var refParentDir = Path.Combine(packsDir, latestVersion.ToString(), "ref");
                    if (Directory.Exists(refParentDir))
                        refDir = Directory.EnumerateDirectories(refParentDir).FirstOrDefault();
                    if (!Directory.Exists(refDir)) return count;
                }

                foreach (var dllPath in Directory.EnumerateFiles(refDir, "*.dll"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(dllPath);
                    if (string.IsNullOrEmpty(fileName)) continue;
                    if (!_allowedPrefixes.Any(prefix =>
                            fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    if (names.Contains(fileName)) continue; // 已从 baseDir 加载

                    try
                    {
                        refs.Add(MetadataReference.CreateFromFile(dllPath));
                        paths.Add(dllPath);
                        names.Add(fileName);
                        count++;
                        PluginLogger.Info(
                            $"[MetadataRef] 从 RefPack 补充引用: {fileName} @ {dllPath}",
                            nameof(MetadataReferenceProvider));
                    }
                    catch (Exception ex)
                    {
                        PluginLogger.Warning(
                            $"[MetadataRef] RefPack 加载失败: Name={fileName}, 错误={ex.Message}",
                            nameof(MetadataReferenceProvider));
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warning(
                    $"[MetadataRef] RefPack 扫描异常: {ex.Message}",
                    nameof(MetadataReferenceProvider));
            }
            return count;
        }
    }
}
