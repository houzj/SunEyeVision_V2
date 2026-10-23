using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>引用先于缓存校验，成功产物按完整内容身份缓存，失败不进入成功缓存。</summary>
    internal sealed class ScriptCompiler
    {
        private static volatile bool _warmupStarted;
        private static readonly object _warmupLock = new();

        /// <summary>
        /// 启动预热 Roslyn Emit 引擎（代码无关）。
        /// 原理：Emit 耗时大头是编译器引擎首次初始化（IL 生成器/元数据写入器冷启动，实测 ~947ms），
        /// 与编译什么代码无关。启动时后台编译一次最小模板，引擎热起来后，用户编译任意脚本
        /// Emit 都从 ~947ms 降到 ~29ms（代码改动不影响，热的是引擎不是结果）。
        /// 幂等：进程内只预热一次；失败仅告警不阻塞（fire-and-forget）。
        /// </summary>
        public static void Warmup()
        {
            lock (_warmupLock)
            {
                if (_warmupStarted) return;
                _warmupStarted = true;
            }

            _ = Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    // 最小模板：继承 Script 基类，仅触发完整编译管线（含 Emit 引擎初始化）
                    const string warmupCode = @"
using SunEyeVision.Plugin.SDK.Scripting;
public partial class WarmupScript : Script
{
    public override void Init() { }
    public override void Process() { }
}";
                    var result = new ScriptCompiler().Compile(
                        ["", warmupCode],
                        "ScriptWarmup",
                        null,
                        MetadataReferenceProvider.RequiredAssemblyNames);
                    sw.Stop();
                    if (result.Success)
                    {
                        PluginLogger.Info(
                            $"[ScriptCompiler] 启动预热 Emit 引擎完成: {sw.ElapsedMilliseconds}ms",
                            nameof(ScriptCompiler));
                    }
                    else
                    {
                        PluginLogger.Warning(
                            $"[ScriptCompiler] 启动预热 Emit 引擎编译失败: {string.Join(";", result.Diagnostics)}",
                            nameof(ScriptCompiler));
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    PluginLogger.Warning(
                        $"[ScriptCompiler] 启动预热 Emit 引擎异常: {ex.Message}",
                        nameof(ScriptCompiler));
                }
            });
        }


        public sealed class CompileResult
        {
            public bool Success { get; set; }
            public byte[]? AssemblyBytes { get; set; }
            public byte[]? SymbolBytes { get; set; }
            public List<string> Diagnostics { get; set; } = [];
            public string? EntryClassName { get; set; }
            public int UsingLineOffset { get; set; }
            public string CacheKey { get; set; } = "";
            public ScriptReferenceSnapshot? ReferenceSnapshot { get; set; }
            public bool CacheHit { get; set; }
            public bool JoinedInFlight { get; set; }
            public double WaitMs { get; set; }
            public double ReferenceMs { get; set; }
            public double ParseMs { get; set; }
            public double EmitMs { get; set; }
            public double TotalMs { get; set; }
        }
        private const int MaxCachedCompilations = 32;
        private static readonly ConcurrentDictionary<string, CompileResult> _compileCache = new();
        private static readonly object CacheGate = new();
        private static readonly CSharpParseOptions _parseOptions = new(LanguageVersion.Latest);
        private static readonly CSharpCompilationOptions _compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release).WithAllowUnsafe(true);
        internal static string ComputeCompilationKey(IReadOnlyList<string> parts, ScriptReferenceSnapshot snapshot)
            => ScriptReferenceSnapshot.Hash(new[] { "script-contract-v1", snapshot.Identity,
                typeof(CSharpCompilation).Assembly.FullName!, typeof(CSharpCompilation).Assembly.ManifestModule.ModuleVersionId.ToString(),
                "Latest|Regular|DLL|Release|Unsafe|PortablePdb|UTF8" }.Concat(parts));

        public CompileResult Compile(IReadOnlyList<string> codeParts, string assemblyName,
            string? assemblyRefDir = null, IReadOnlyList<string>? assemblyNames = null)
            => CompileAsync(codeParts, assemblyName, assemblyRefDir, assemblyNames).GetAwaiter().GetResult();

        public async Task<CompileResult> CompileAsync(IReadOnlyList<string> codeParts, string assemblyName,
            string? assemblyRefDir = null, IReadOnlyList<string>? assemblyNames = null, CancellationToken token = default)
        {
            var total = Stopwatch.StartNew();
            var parts = codeParts.ToArray();
            var names = assemblyNames?.ToArray();
            try
            {
                var snapshot = await Task.Run(() => ScriptReferenceSnapshot.Capture(names, assemblyRefDir), token).ConfigureAwait(false);
                var key = ComputeCompilationKey(parts, snapshot);
                var cached = GetCachedResult(key);
                if (cached != null)
                {
                    cached.CacheHit = true; cached.ReferenceMs = snapshot.PreparationMs;
                    cached.ParseMs = cached.EmitMs = cached.WaitMs = 0;
                    cached.TotalMs = snapshot.PreparationMs;
                    Log(cached); return cached;
                }
                var result = await ScriptCompilationCoordinator.GetAsync(key, () => CompileCore(parts, snapshot), token).ConfigureAwait(false);
                result.ReferenceMs = snapshot.PreparationMs;
                result.TotalMs = total.Elapsed.TotalMilliseconds;
                return result;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException)
            {
                return new CompileResult { Diagnostics = [$"脚本引用或编译准备失败: {ex.Message}"] };
            }
        }

        private CompileResult CompileCore(IReadOnlyList<string> codeParts, ScriptReferenceSnapshot snapshot)
        {
            var timer = Stopwatch.StartNew();
            var result = new CompileResult();
            if (codeParts == null || codeParts.Count == 0 || codeParts.All(string.IsNullOrWhiteSpace))
            {
                result.Diagnostics.Add("脚本代码为空");
                return result;
            }
            var parts = codeParts.ToArray();
            try
            {
                result.ReferenceSnapshot = snapshot;
                result.ReferenceMs = snapshot.PreparationMs;
                result.CacheKey = ComputeCompilationKey(parts, snapshot);
                if (_compileCache.TryGetValue(result.CacheKey, out var cached))
                {
                    result = Copy(cached);
                    result.CacheHit = true;
                    result.ReferenceMs = snapshot.PreparationMs;
                    result.ParseMs = result.EmitMs = 0;
                    result.TotalMs = timer.Elapsed.TotalMilliseconds;
                    Log(result);
                    return result;
                }
                var parseTimer = Stopwatch.StartNew();
                var trees = parts.Select((code, index) => SyntaxFactory.ParseSyntaxTree(
                    SourceText.From(code, Encoding.UTF8), _parseOptions,
                    path: index == 0 ? "ScriptGenerated.cs" : $"ScriptUser{index}.cs")).ToArray();
                result.ParseMs = parseTimer.Elapsed.TotalMilliseconds;
                var compilation = CSharpCompilation.Create("Script_" + result.CacheKey,
                    trees, snapshot.AnalysisReferences.References, _compilationOptions);
                using var assemblyStream = new MemoryStream();
                using var symbolStream = new MemoryStream();
                var emitTimer = Stopwatch.StartNew();
                Interlocked.Increment(ref ScriptCompilationCoordinator.EmitOperations);
                if (ScriptCompilationCoordinator.EmissionCounts.Count >= 256) ScriptCompilationCoordinator.EmissionCounts.Clear();
                ScriptCompilationCoordinator.EmissionCounts.AddOrUpdate(result.CacheKey, 1, (_, count) => count + 1);
                var emitted = compilation.Emit(assemblyStream, symbolStream,
                    options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
                result.EmitMs = emitTimer.Elapsed.TotalMilliseconds;
                result.Success = emitted.Success;
                if (result.Success)
                {
                    result.AssemblyBytes = assemblyStream.ToArray();
                    result.SymbolBytes = symbolStream.ToArray();
                    result.EntryClassName = ExtractClassName(string.Join("\n", parts));
                    lock (CacheGate)
                    {
                        if (_compileCache.Count >= MaxCachedCompilations) _compileCache.Clear();
                        _compileCache[result.CacheKey] = Copy(result);
                    }
                }
                else
                    foreach (var diagnostic in emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    {
                        var line = diagnostic.Location.GetLineSpan();
                        result.Diagnostics.Add($"错误 ({line.Path}, 行 {line.StartLinePosition.Line + 1}, 列 {line.StartLinePosition.Character + 1}): {diagnostic.GetMessage()}");
                    }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException)
            {
                result.Success = false;
                result.Diagnostics.Add($"脚本引用或编译准备失败: {ex.Message}");
            }
            result.TotalMs = timer.Elapsed.TotalMilliseconds;
            Log(result);
            return result;
        }
        private static void Log(CompileResult result) => PluginLogger.Info(
            $"[ScriptCompiler] key={result.CacheKey}, refs={result.ReferenceSnapshot?.Identity}, cacheHit={result.CacheHit}, " +
            $"referenceMs={result.ReferenceMs:F2}, parseMs={result.ParseMs:F2}, emitMs={result.EmitMs:F2}, totalMs={result.TotalMs:F2}, success={result.Success}", nameof(ScriptCompiler));
        internal static CompileResult Copy(CompileResult value) => new()
        {
            Success = value.Success, AssemblyBytes = value.AssemblyBytes?.ToArray(), SymbolBytes = value.SymbolBytes?.ToArray(),
            Diagnostics = new List<string>(value.Diagnostics), EntryClassName = value.EntryClassName,
            UsingLineOffset = value.UsingLineOffset, CacheKey = value.CacheKey, ReferenceSnapshot = value.ReferenceSnapshot,
            ReferenceMs = value.ReferenceMs, ParseMs = value.ParseMs, EmitMs = value.EmitMs, TotalMs = value.TotalMs,
            CacheHit = value.CacheHit, JoinedInFlight = value.JoinedInFlight, WaitMs = value.WaitMs
        };
        internal static CompileResult? GetCachedResult(string cacheKey)
            => _compileCache.TryGetValue(cacheKey, out var value) ? Copy(value) : null;

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
