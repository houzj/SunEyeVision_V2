using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

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
            var result = new CompileResult();

            if (string.IsNullOrWhiteSpace(code))
            {
                result.Diagnostics.Add("脚本代码为空");
                return result;
            }

            // 1. 不再自动注入 using — 补全和编译由脚本中的显式 using 语句驱动
            result.UsingLineOffset = 0;
            var fullCode = code;

            // 2. 构建语法树
            var sourceText = SourceText.From(fullCode);
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText,
                new CSharpParseOptions(LanguageVersion.Latest));

            // 3. 扫描引用目录下所有 DLL 作为元数据引用
            var metadataReferences = new List<MetadataReference>();
            try
            {
                if (!string.IsNullOrEmpty(assemblyRefDir) && Directory.Exists(assemblyRefDir))
                {
                    foreach (var dll in Directory.EnumerateFiles(assemblyRefDir, "*.dll"))
                    {
                        if (File.Exists(dll))
                            metadataReferences.Add(MetadataReference.CreateFromFile(dll));
                    }
                }
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add($"扫描引用目录失败: {assemblyRefDir} - {ex.Message}");
            }

            // 4. 创建编译
            var options = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release)
                .WithAllowUnsafe(true);

            var compilation = CSharpCompilation.Create(
                assemblyName,
                [syntaxTree],
                metadataReferences,
                options);

            // 5. 编译并输出
            using var assemblyStream = new MemoryStream();
            using var symbolStream = new MemoryStream();

            var emitResult = compilation.Emit(assemblyStream, symbolStream);

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
