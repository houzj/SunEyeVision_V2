using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 共享元数据引用缓存 — AdhocWorkspace（IntelliSense）和 CSharpCompilation（Emit）共用同一份引用。
    /// 消除重复的 MetadataReference.CreateFromFile 加载，降低内存占用。
    /// </summary>
    internal static class SharedMetadataReferenceProvider
    {
        /// <summary>
        /// ScriptRefs 目录引用缓存（按目录缓存，目录变化时重新加载）
        /// </summary>
        private static List<MetadataReference>? _cachedScriptRefs;
        private static string? _lastRefDir;

        /// <summary>
        /// 获取 ScriptRefs 目录的元数据引用（跳过非托管 DLL，按目录缓存）
        /// </summary>
        public static List<MetadataReference> GetScriptRefs(string refDir)
        {
            if (_cachedScriptRefs != null && _lastRefDir == refDir)
            {
                PluginLogger.Info($"[SharedMetadataRef] ScriptRefs 缓存命中: {_cachedScriptRefs.Count} 个引用",
                    nameof(SharedMetadataReferenceProvider));
                return _cachedScriptRefs;
            }

            if (!Directory.Exists(refDir))
            {
                PluginLogger.Warning($"[SharedMetadataRef] 引用目录不存在: {refDir}",
                    nameof(SharedMetadataReferenceProvider));
                _cachedScriptRefs = [];
                _lastRefDir = refDir;
                return _cachedScriptRefs;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var refs = new List<MetadataReference>();
            int totalDlls = 0, skipped = 0;

            foreach (var dll in Directory.EnumerateFiles(refDir, "*.dll"))
            {
                totalDlls++;
                if (!IsManagedAssembly(dll))
                {
                    skipped++;
                    continue;
                }
                try
                {
                    refs.Add(MetadataReference.CreateFromFile(dll));
                }
                catch (Exception ex)
                {
                    PluginLogger.Warning($"[SharedMetadataRef] 加载 DLL 失败: {dll}, 错误: {ex.Message}",
                        nameof(SharedMetadataReferenceProvider));
                    skipped++;
                }
            }

            sw.Stop();
            PluginLogger.Info(
                $"[SharedMetadataRef] ScriptRefs 加载: 总DLL={totalDlls}, 已加载={refs.Count}, 跳过={skipped}, 耗时={sw.ElapsedMilliseconds}ms",
                nameof(SharedMetadataReferenceProvider));

            _cachedScriptRefs = refs;
            _lastRefDir = refDir;
            return refs;
        }

        /// <summary>
        /// 判断 DLL 是否为有效托管程序集（跳过原生 C++ DLL，避免 Roslyn 编译报 CS0006）。
        /// 使用 AssemblyName.GetAssemblyName：既能排除原生 C++ DLL，也能排除无有效 .NET 元数据的文件。
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
