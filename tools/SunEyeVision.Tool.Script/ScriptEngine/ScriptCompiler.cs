using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        // 缓存区：进程生命周期内不变的对象
        // =====================================================

        /// <summary>
        /// 默认 ALC 中脚本相关的程序集引用（进程生命周期内不变，懒初始 + 过滤）
        /// </summary>
        private static List<MetadataReference>? _defaultReferences;

        /// <summary>
        /// 判断程序集是否与脚本编译/补全相关（排除 WPF、Roslyn 自身、Win32/UI 等）
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

            // 其他（包括第三方 NuGet 如 Humanizer、Newtonsoft.Json 等）
            // 排除已知的 WPF/UI/Windows 程序集
            return !assemblyName.StartsWith("Presentation")
                && !assemblyName.StartsWith("UIAutomation")
                && assemblyName != "WindowsBase"
                && assemblyName != "Accessibility"
                && assemblyName != "DirectWriteForwarder"
                && assemblyName != "ReachFramework"
                && !assemblyName.StartsWith("Avalon");
        }

        private static List<MetadataReference> GetDefaultReferences()
        {
            if (_defaultReferences == null)
            {
                var sw = Stopwatch.StartNew();
                var refs = new List<MetadataReference>();
                int totalAsm = 0, filteredCount = 0;
                foreach (var asm in AssemblyLoadContext.Default.Assemblies)
                {
                    totalAsm++;
                    if (asm.IsDynamic) continue;
                    if (!IsScriptRelevantAssembly(asm.GetName().Name)) continue;
                    filteredCount++;
                    try
                    {
                        refs.Add(MetadataReference.CreateFromFile(asm.Location));
                    }
                    catch
                    {
                        // 跳过无法创建引用的程序集（如原生镜像）
                    }
                }
                _defaultReferences = refs;
                sw.Stop();
                PluginLogger.Info($"[ScriptCompiler] 程序集引用首次初始化: ALC总={totalAsm}, 过滤后={filteredCount}, 成功创建={refs.Count}, 耗时={sw.ElapsedMilliseconds}ms",
                    nameof(ScriptCompiler));
            }
            else
            {
                PluginLogger.Info($"[ScriptCompiler] 程序集引用缓存命中: {_defaultReferences.Count} 个引用",
                    nameof(ScriptCompiler));
            }
            return _defaultReferences;
        }

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
        /// ScriptRefs 目录引用缓存（按目录缓存，目录变化时重新加载）
        /// </summary>
        private List<MetadataReference>? _cachedScriptRefs;
        private string? _lastRefDir;

        /// <summary>
        /// 编译 C# 脚本代码
        /// </summary>
        /// <param name="code">C# 代码</param>
        /// <param name="assemblyName">程序集名称（每次不同，避免 ALC 缓存冲突）</param>
        /// <param name="assemblyRefDir">程序集引用目录路径，目录下所有 *.dll 均为引用</param>
        /// <returns>编译结果</returns>
        public CompileResult Compile(
            string code,
            string assemblyName,
            string? assemblyRefDir = null)
        {
            var totalSw = Stopwatch.StartNew();
            var result = new CompileResult();

            if (string.IsNullOrWhiteSpace(code))
            {
                result.Diagnostics.Add("脚本代码为空");
                return result;
            }

            // 1. 不再自动注入 using — 补全和编译由脚本中的显式 using 语句驱动
            result.UsingLineOffset = 0;
            var fullCode = code;

            // 2. 构建语法树（使用缓存的 ParseOptions）
            var parseSw = Stopwatch.StartNew();
            var sourceText = SourceText.From(fullCode);
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText, _parseOptions);
            parseSw.Stop();

            // 3. 获取元数据引用：默认 ALC（缓存） + ScriptRefs（按目录缓存）
            var refSw = Stopwatch.StartNew();
            var defaultRefs = GetDefaultReferences();
            var metadataReferences = new List<MetadataReference>(defaultRefs.Count + 16);
            metadataReferences.AddRange(defaultRefs);

            // 3b. ScriptRefs 目录（用户自定义第三方 DLL），有变化才重新扫描
            int scriptRefCount = 0;
            if (!string.IsNullOrEmpty(assemblyRefDir) && Directory.Exists(assemblyRefDir))
            {
                if (_cachedScriptRefs == null || _lastRefDir != assemblyRefDir)
                {
                    try
                    {
                        _cachedScriptRefs = Directory.EnumerateFiles(assemblyRefDir, "*.dll")
                            .Select(dll => MetadataReference.CreateFromFile(dll))
                            .Cast<MetadataReference>()
                            .ToList();
                        _lastRefDir = assemblyRefDir;
                    }
                    catch (Exception ex)
                    {
                        result.Diagnostics.Add($"扫描引用目录失败: {assemblyRefDir} - {ex.Message}");
                        _cachedScriptRefs = [];
                    }
                }
                scriptRefCount = _cachedScriptRefs.Count;
                metadataReferences.AddRange(_cachedScriptRefs);
            }
            refSw.Stop();

            // 4. 创建编译（使用缓存的 CompilationOptions）
            var createSw = Stopwatch.StartNew();
            var compilation = CSharpCompilation.Create(
                assemblyName,
                [syntaxTree],
                metadataReferences,
                _compilationOptions);
            createSw.Stop();

            // 5. 编译并输出
            var emitSw = Stopwatch.StartNew();
            using var assemblyStream = new MemoryStream();
            using var symbolStream = new MemoryStream();

            var emitResult = compilation.Emit(assemblyStream, symbolStream);
            emitSw.Stop();

            totalSw.Stop();

            PluginLogger.Info($"[ScriptCompiler] 编译性能: 语法树={parseSw.ElapsedMilliseconds}ms, " +
                $"引用={refSw.ElapsedMilliseconds}ms(默认={defaultRefs.Count},ScriptRefs={scriptRefCount}), " +
                $"创建编译={createSw.ElapsedMilliseconds}ms, Emit={emitSw.ElapsedMilliseconds}ms, " +
                $"总={totalSw.ElapsedMilliseconds}ms, 成功={emitResult.Success}, " +
                $"代码长度={code.Length}",
                nameof(ScriptCompiler));

            if (emitResult.Success)
            {
                result.Success = true;
                result.AssemblyBytes = assemblyStream.ToArray();
                result.SymbolBytes = symbolStream.ToArray();
                result.EntryClassName = ExtractClassName(code);
            }
            else
            {
                foreach (var diagnostic in emitResult.Diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                    {
                        var location = diagnostic.Location.GetLineSpan();
                        var actualLine = location.StartLinePosition.Line + 1 - result.UsingLineOffset;
                        result.Diagnostics.Add(
                            $"错误 (行 {actualLine}, 列 {location.StartLinePosition.Character + 1}): {diagnostic.GetMessage()}");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 从代码中提取继承 Script 的类名
        /// </summary>
        private static string? ExtractClassName(string code)
        {
            // 简单解析：查找 "class XXX : Script" 模式
            var lines = code.Split('\n');
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
                            var ns = ExtractNamespace(code);
                            var className = afterClass.Substring(0, colonIndex).Trim();
                            return string.IsNullOrEmpty(ns) ? className : $"{ns}.{className}";
                        }
                    }
                }
            }
            return null;
        }

        private static string ExtractNamespace(string code)
        {
            var lines = code.Split('\n');
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
