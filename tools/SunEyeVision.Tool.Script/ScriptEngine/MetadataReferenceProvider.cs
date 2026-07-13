using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 统一元数据引用服务 — 从 .NET SDK RefPack 加载 BCL 引用程序集，
    /// 为 Roslyn Workspace（UI 高亮）和 CSharpCompilation（编译 Emit）提供类型元数据。
    /// 业务 DLL（halcondotnetxl/OpenCvSharp/用户自定义）由 ScriptRefs 目录独立管理。
    ///
    /// Roslyn 引用 = RefPack（BCL，~165 个） + ScriptRefs（业务，用户自定义）。
    /// 不再扫描 AppBase 目录，消除白名单维护成本和路径重复问题。
    /// </summary>
    internal static class MetadataReferenceProvider
    {
        private static readonly object _lock = new();
        private static ImmutableList<MetadataReference>? _cachedReferences;
        private static ImmutableHashSet<string>? _cachedPaths;
        private static ImmutableHashSet<string>? _cachedNames;

        /// <summary>
        /// RefPack 排除名单 — RefPack 目录下所有 DLL 都是合法 .NET 引用程序集，
        /// 不应再加白名单过滤。唯独 System.Private.CoreLib 是实现程序集
        /// （带 ReferenceAssembly 标记），不能直接作为 Roslyn MetadataReference。
        /// </summary>
        private static readonly string[] _refPackBlockedNames =
        [
            "System.Private.CoreLib",
        ];

        /// <summary>
        /// 获取所有引用程序集的元数据引用（缓存），用于 Roslyn Workspace 和 CSharpCompilation。
        /// 当前仅包含 .NET SDK RefPack 的 BCL 引用程序集。
        /// 业务 DLL（halcondotnetxl/OpenCvSharp/用户自定义）由 ScriptRefs 独立管理。
        /// </summary>
        public static ImmutableList<MetadataReference> GetHostReferences()
        {
            EnsureLoaded();
            return _cachedReferences!;
        }

        /// <summary>
        /// 获取 RefPack 引用程序集的文件路径（仅用于诊断，不用于运行时加载）。
        /// RefPack 程序集为 metadata-only 引用程序集，不可在运行时 LoadFromAssemblyPath。
        /// </summary>
        public static ImmutableHashSet<string> GetHostAssemblyPaths()
        {
            EnsureLoaded();
            return _cachedPaths!;
        }

        /// <summary>
        /// 获取 RefPack 引用程序集的简单名称（仅用于诊断）。
        /// </summary>
        public static ImmutableHashSet<string> GetHostAssemblyNames()
        {
            EnsureLoaded();
            return _cachedNames!;
        }

        /// <summary>
        /// 清除缓存，下次调用 EnsureLoaded 时重新加载。
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
                var refs = ImmutableList.CreateBuilder<MetadataReference>();
                var paths = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
                var names = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);
                int count = 0;

                // 从 .NET SDK Ref Pack 加载运行时核心引用（如 System.Private.CoreLib）
                // 这些程序集不在发布输出目录中，但 Roslyn 编译需要它们来识别
                // System.Void、System.Int32 等预定义类型（修复 CS0518 级联误报）
                count = TryLoadRefPackFallback(refs, paths, names);

                _cachedReferences = refs.ToImmutable();
                _cachedPaths = paths.ToImmutable();
                _cachedNames = names.ToImmutable();
                sw.Stop();

                PluginLogger.Info(
                    $"[MetadataRef] 统一引用初始化完成: RefPack补充={count}, 耗时={sw.ElapsedMilliseconds}ms",
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

                    // RefPack 目录下所有 DLL 都是合法的 .NET 引用程序集，
                    // 唯独排除 System.Private.CoreLib（实现程序集，不应作为 MetadataReference）。
                    if (_refPackBlockedNames.Any(name =>
                            string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)))
                        continue;

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
