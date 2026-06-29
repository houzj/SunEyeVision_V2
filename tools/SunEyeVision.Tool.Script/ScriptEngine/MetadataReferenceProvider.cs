using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 统一元数据引用服务 — 进程生命周期内单例，扫描 AssemblyLoadContext.Default 中
    /// 与脚本相关的所有程序集，为 Roslyn Workspace（UI 高亮）、CSharpCompilation（编译 Emit）、
    /// AssemblyLoadContext（沙箱加载）提供同一份宿主程序集可见性（SSOT）。
    ///
    /// 设计动机：消除"所见非所得"——此前 5 个决策点（UI 高亮 / UI 诊断 / 编译 Emit / 沙箱解析 /
    /// 运行时执行）各自决定程序集来源，必然漂移。统一服务作为唯一决策点，强制一致。
    /// </summary>
    internal static class MetadataReferenceProvider
    {
        private static readonly object _lock = new();
        private static ImmutableList<MetadataReference>? _cachedReferences;
        private static ImmutableHashSet<string>? _cachedPaths;
        private static ImmutableHashSet<string>? _cachedNames;

        /// <summary>
        /// 获取当前默认 ALC 中与脚本相关的所有程序集的元数据引用（缓存）。
        /// 适用于 Roslyn Workspace（UI 高亮）与 CSharpCompilation（编译）。
        /// </summary>
        public static ImmutableList<MetadataReference> GetHostReferences()
        {
            EnsureLoaded();
            return _cachedReferences!;
        }

        /// <summary>
        /// 获取所有宿主程序集的文件路径（绝对路径集合）— 供沙箱 ALC 按路径精确加载。
        /// </summary>
        public static ImmutableHashSet<string> GetHostAssemblyPaths()
        {
            EnsureLoaded();
            return _cachedPaths!;
        }

        /// <summary>
        /// 获取所有宿主程序集的简单名称（不含版本/公钥）— 供沙箱 OnResolving 名称匹配。
        /// </summary>
        public static ImmutableHashSet<string> GetHostAssemblyNames()
        {
            EnsureLoaded();
            return _cachedNames!;
        }

        /// <summary>
        /// 强制重新扫描 ALC（仅在宿主动态加载新程序集时使用，当前版本不调用，保留 API）。
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
                int totalAsm = 0, filteredCount = 0, failed = 0;

                foreach (var asm in AssemblyLoadContext.Default.Assemblies)
                {
                    totalAsm++;
                    if (asm.IsDynamic) continue;
                    var name = asm.GetName().Name;
                    if (name == null) continue;
                    if (!IsScriptRelevantAssembly(name)) continue;
                    if (string.IsNullOrEmpty(asm.Location)) continue;

                    try
                    {
                        refs.Add(MetadataReference.CreateFromFile(asm.Location));
                        paths.Add(asm.Location);
                        names.Add(name);
                        filteredCount++;
                    }
                    catch (Exception ex)
                    {
                        PluginLogger.Warning(
                            $"[MetadataRef] 创建引用失败被跳过: Name={name}, Location={asm.Location}, 错误={ex.Message}",
                            nameof(MetadataReferenceProvider));
                        failed++;
                    }
                }

                _cachedReferences = refs.ToImmutable();
                _cachedPaths = paths.ToImmutable();
                _cachedNames = names.ToImmutable();
                sw.Stop();

                PluginLogger.Info(
                    $"[MetadataRef] 统一引用初始化完成: ALC总={totalAsm}, 过滤后={filteredCount}, 失败={failed}, 耗时={sw.ElapsedMilliseconds}ms",
                    nameof(MetadataReferenceProvider));
            }
        }

        /// <summary>
        /// 判断程序集是否与脚本编译/补全相关（排除 WPF、Roslyn 自身、Win32/UI 等）。
        /// 从 ScriptCompiler 迁出，作为统一过滤策略。
        /// </summary>
        private static bool IsScriptRelevantAssembly(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName)) return false;

            // 始终包含：核心运行时
            if (assemblyName is "System.Runtime" or "System.Private.CoreLib"
                or "netstandard" or "mscorlib")
                return true;

            // System.*（排除 WPF/UI 相关）
            if (assemblyName.StartsWith("System.") || assemblyName == "System")
                return !assemblyName.StartsWith("System.Windows")
                    && !assemblyName.StartsWith("System.Xaml");

            // Microsoft.*（排除 Win32/WinUI/Roslyn 等）
            if (assemblyName.StartsWith("Microsoft."))
                return !assemblyName.StartsWith("Microsoft.Win32")
                    && !assemblyName.StartsWith("Microsoft.UI")
                    && assemblyName != "Microsoft.CodeAnalysis"
                    && !assemblyName.StartsWith("Microsoft.CodeAnalysis.");

            // 项目程序集
            if (assemblyName.StartsWith("SunEyeVision."))
                return true;

            // HalconDotNet（halcondotnet / halcondotnetxl）
            if (assemblyName.StartsWith("halcondotnet"))
                return true;

            // 其他（包括第三方 NuGet 如 Humanizer、Newtonsoft.Json 等）
            return !assemblyName.StartsWith("Presentation")
                && !assemblyName.StartsWith("UIAutomation")
                && assemblyName != "WindowsBase"
                && assemblyName != "Accessibility"
                && assemblyName != "DirectWriteForwarder"
                && assemblyName != "ReachFramework"
                && !assemblyName.StartsWith("Avalon");
        }
    }
}
