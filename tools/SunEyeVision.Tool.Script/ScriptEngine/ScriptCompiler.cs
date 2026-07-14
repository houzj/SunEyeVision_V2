using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 脚本编译器 — 使用 Roslyn 编译 C# 代码为程序集。
    /// 程序集引用全部从指定目录下的 *.dll 文件扫描获取。
    /// </summary>
    internal sealed class ScriptCompiler
    {

        /// <summary>
        /// 编译结果
        /// </summary>
        public sealed class CompileResult
        {
            public bool Success { get; set; }
            public byte[]? AssemblyBytes { get; set; }
            public byte[]? SymbolBytes { get; set; }
            public List<string> Diagnostics { get; set; } = [];
            public string? EntryClassName { get; set; }
            public int UsingLineOffset { get; set; }
        }

        // =====================================================
        // 编译结果缓存（按代码哈希 + 引用目录，相同代码跳过 Emit）
        // =====================================================

        /// <summary>
        /// 编译结果缓存（键 = 代码SHA256[:8] + | + 引用目录路径）
        /// </summary>
        private static readonly ConcurrentDictionary<string, CompileResult> _compileCache = new();

        // =====================================================
        // 缓存区：进程生命周期内不变的对象
        // =====================================================

        /// <summary>
        /// 语法解析选项（LanguageVersion.Latest 不变）
        /// </summary>
        private static readonly CSharpParseOptions _parseOptions =
            new CSharpParseOptions(LanguageVersion.Latest);

        /// <summary>
        /// 编译选项（OutputKind/OptimizationLevel 不变）
        /// </summary>
        private static readonly CSharpCompilationOptions _compilationOptions =
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release)
            .WithAllowUnsafe(true);

        /// <summary>
        /// 编译 C# 脚本代码（多代码段，每个代码段构建独立语法树，通过 partial class 合并）
        /// </summary>
        /// <param name="codeParts">多个 C# 代码段（如 [generatedCode, userCode]）</param>
        /// <param name="assemblyName">程序集名称（每次不同，避免 ALC 缓存冲突）</param>
        /// <param name="assemblyRefDir">程序集引用目录路径，目录下所有 *.dll 均为引用</param>
        /// <returns>编译结果</returns>
        public CompileResult Compile(
            IReadOnlyList<string> codeParts,
            string assemblyName,
            string? assemblyRefDir = null)
        {
            var totalSw = Stopwatch.StartNew();
            var result = new CompileResult();
            result.UsingLineOffset = 0;

            if (codeParts == null || codeParts.Count == 0 || codeParts.All(string.IsNullOrWhiteSpace))
            {
                result.Diagnostics.Add("脚本代码为空");
                return result;
            }

            // 合并所有代码段用于缓存哈希 + 类名提取
            var combinedCode = string.Join("\n", codeParts);

            // 0a. 检查编译缓存（相同代码 + 宿主程序集指纹）
            // 缓存键包含宿主已加载程序集的版本指纹：宿主 DLL（SunEyeVision.*/halcon/OpenCvSharp）
            // 升级后指纹变化，自动触发重新编译，避免用旧元数据编译出的 IL 引用已变更的类型。
            var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(combinedCode)));
            var cacheKey = $"{codeHash}|{ComputeHostFingerprint()}";
            if (_compileCache.TryGetValue(cacheKey, out var cached))
            {
                PluginLogger.Info(
                    $"[ScriptCompiler] 编译缓存命中: hash={codeHash[..8]}..., 代码长度={combinedCode.Length}",
                    nameof(ScriptCompiler));
                return new CompileResult
                {
                    Success = cached.Success,
                    AssemblyBytes = cached.AssemblyBytes,
                    SymbolBytes = cached.SymbolBytes,
                    Diagnostics = cached.Diagnostics,
                    EntryClassName = cached.EntryClassName,
                    UsingLineOffset = cached.UsingLineOffset,
                };
            }

            // 2. 构建多个语法树（每个代码段独立语法树）
            var parseSw = Stopwatch.StartNew();
            var syntaxTrees = new SyntaxTree[codeParts.Count];
            for (int i = 0; i < codeParts.Count; i++)
            {
                syntaxTrees[i] = SyntaxFactory.ParseSyntaxTree(
                    SourceText.From(codeParts[i]), _parseOptions);
            }
            parseSw.Stop();

            // 3. 获取元数据引用：宿主已加载程序集（运行时 ALC / TPA，含 BCL 与宿主 API）
            //    + ScriptRefs（用户自定义第三方 DLL）。
            //    BCL（System.Private.CoreLib / System.Runtime / netstandard）由运行时 TPA 保证提供，
            //    不再依赖 .NET SDK RefPack（部署环境可能缺失，且曾错误排除 System.Private.CoreLib 导致 CS0518）。
            var refSw = Stopwatch.StartNew();
            var metadataReferences = new List<MetadataReference>();
            var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int hostRefCount = 0, scriptRefCount = 0;

            foreach (var mdRef in MetadataReferenceProvider.GetHostReferences())
            {
                if (TryAddMetadataRef(metadataReferences, addedPaths, mdRef)) hostRefCount++;
            }

            // 3b. ScriptRefs 目录（仅用户自定义第三方 DLL），通过共享缓存加载
            if (!string.IsNullOrEmpty(assemblyRefDir))
            {
                var sharedRefs = SharedMetadataReferenceProvider.GetScriptRefs(assemblyRefDir);
                scriptRefCount = sharedRefs.Count;
                foreach (var mdRef in sharedRefs)
                {
                    TryAddMetadataRef(metadataReferences, addedPaths, mdRef);
                }
            }
            refSw.Stop();

            // 4. 创建编译（传入多个语法树）
            var createSw = Stopwatch.StartNew();
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees,
                metadataReferences,
                _compilationOptions);
            createSw.Stop();

            // 5. 编译并输出
            var emitSw = Stopwatch.StartNew();
            using var assemblyStream = new MemoryStream();
            using var symbolStream = new MemoryStream();

            var emitResult = compilation.Emit(assemblyStream, symbolStream,
                options: new EmitOptions(
                    debugInformationFormat: DebugInformationFormat.PortablePdb));
            emitSw.Stop();

            totalSw.Stop();

            PluginLogger.Info($"[ScriptCompiler] 编译性能: 语法树={codeParts.Count}({parseSw.ElapsedMilliseconds}ms), " +
                $"引用={refSw.ElapsedMilliseconds}ms(宿主={hostRefCount},ScriptRefs={scriptRefCount}), " +
                $"创建编译={createSw.ElapsedMilliseconds}ms, Emit={emitSw.ElapsedMilliseconds}ms, " +
                $"总={totalSw.ElapsedMilliseconds}ms, 成功={emitResult.Success}, " +
                $"代码长度={combinedCode.Length}",
                nameof(ScriptCompiler));

            if (emitResult.Success)
            {
                result.Success = true;
                result.AssemblyBytes = assemblyStream.ToArray();
                result.SymbolBytes = symbolStream.ToArray();
                result.EntryClassName = ExtractClassName(combinedCode);

                // 编译成功，加入缓存
                _compileCache[cacheKey] = result;
            }
            else
            {
                foreach (var diagnostic in emitResult.Diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                    {
                        var location = diagnostic.Location.GetLineSpan();
                        // 行号相对于所在语法树，已自然对应各代码段内的行号
                        var actualLine = location.StartLinePosition.Line + 1;
                        result.Diagnostics.Add(
                            $"错误 (行 {actualLine}, 列 {location.StartLinePosition.Character + 1}): {diagnostic.GetMessage()}");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 按元数据引用的文件路径去重后加入引用列表，避免重复引用导致 Roslyn CS1703。
        /// 文件型引用以规范化全路径为键；无路径的引用（如内存引用）直接加入。
        /// </summary>
        private static bool TryAddMetadataRef(
            List<MetadataReference> list, HashSet<string> addedPaths, MetadataReference mdRef)
        {
            var p = (mdRef as PortableExecutableReference)?.FilePath;
            if (p != null)
            {
                string key;
                try { key = Path.GetFullPath(p); }
                catch { key = p; }
                if (!addedPaths.Add(key)) return false;
            }
            list.Add(mdRef);
            return true;
        }

        /// <summary>
        /// 从合并的代码中提取继承 Script 的类名
        /// </summary>
        private static string? ExtractClassName(string combinedCode)
        {
            // 简单解析：查找 "class XXX : Script" 模式
            var lines = combinedCode.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Contains(": Script"))
                {
                    var classIndex = trimmed.IndexOf("class ", StringComparison.Ordinal);
                    if (classIndex >= 0)
                    {
                        var afterClass = trimmed.Substring(classIndex + "class ".Length);
                        var colonIndex = afterClass.IndexOf(" :", StringComparison.Ordinal);
                        if (colonIndex > 0)
                        {
                            var ns = ExtractNamespace(combinedCode);
                            var className = afterClass.Substring(0, colonIndex).Trim();
                            return string.IsNullOrEmpty(ns) ? className : $"{ns}.{className}";
                        }
                    }
                }
            }
            return null;
        }

        private static string ExtractNamespace(string combinedCode)
        {
            var lines = combinedCode.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("namespace "))
                {
                    return trimmed.Substring("namespace ".Length).Trim().TrimEnd(';', '{');
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// 计算宿主程序集版本指纹（用于编译缓存键）。
        /// 仅关注脚本实际依赖的宿主 DLL（SunEyeVision.* / halcondotnetxl / OpenCvSharp / System.Windows.Forms），
        /// 这些 DLL 升级会改变 API 表面，必须触发脚本重新编译。
        /// BCL 程序集版本在进程生命周期内不变，不计入指纹以保证缓存命中率。
        /// </summary>
        private static string ComputeHostFingerprint()
        {
            var nativeHostNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "halcondotnetxl",
                "OpenCvSharp",
                "System.Windows.Forms",
            };

            var sb = new StringBuilder();
            foreach (var asm in AssemblyLoadContext.Default.Assemblies
                .Where(a => !a.IsDynamic && a.GetName().Name != null)
                .Where(a =>
                    a.GetName().Name!.StartsWith("SunEyeVision.", StringComparison.OrdinalIgnoreCase)
                    || nativeHostNames.Contains(a.GetName().Name!))
                .OrderBy(a => a.GetName().Name))
            {
                var name = asm.GetName();
                sb.Append($"{name.Name}:{name.Version}|");
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
        }

    }
}
