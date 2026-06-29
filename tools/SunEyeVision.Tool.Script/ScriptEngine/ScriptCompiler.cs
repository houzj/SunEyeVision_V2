using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            // 0a. 检查编译缓存（相同代码 + 相同引用目录）
            var codeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(combinedCode)));
            var cacheKey = $"{codeHash}|{assemblyRefDir ?? ""}";
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

            // 3. 获取元数据引用：默认 ALC（统一服务缓存） + ScriptRefs（按目录缓存）
            var refSw = Stopwatch.StartNew();
            var defaultRefs = MetadataReferenceProvider.GetHostReferences();
            var metadataReferences = new List<MetadataReference>(defaultRefs.Count + 16);
            metadataReferences.AddRange(defaultRefs);

            // 3b. ScriptRefs 目录（用户自定义第三方 DLL），通过共享缓存加载
            int scriptRefCount = 0;
            if (!string.IsNullOrEmpty(assemblyRefDir))
            {
                var sharedRefs = SharedMetadataReferenceProvider.GetScriptRefs(assemblyRefDir);
                scriptRefCount = sharedRefs.Count;
                metadataReferences.AddRange(sharedRefs);
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
                $"引用={refSw.ElapsedMilliseconds}ms(默认={defaultRefs.Count},ScriptRefs={scriptRefCount}), " +
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

    }
}
