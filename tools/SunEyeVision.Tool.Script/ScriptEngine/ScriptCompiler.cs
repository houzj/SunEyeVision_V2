using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 脚本编译器 — 使用 Roslyn 编译 C# 代码为程序集。
    /// 自动注入基础 using 和 DLL 引用。
    /// </summary>
    internal sealed class ScriptCompiler
    {
        private static readonly string[] DefaultUsings =
        [
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Threading.Tasks",
            "System.Drawing",
            "SunEyeVision.Tool.Script.ScriptEngine"
        ];

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
        /// <param name="additionalAssemblies">额外引用的 DLL 路径列表</param>
        /// <returns>编译结果</returns>
        public CompileResult Compile(
            string code,
            string assemblyName,
            List<string>? additionalAssemblies = null)
        {
            var result = new CompileResult();

            if (string.IsNullOrWhiteSpace(code))
            {
                result.Diagnostics.Add("脚本代码为空");
                return result;
            }

            // 1. 预置 using 语句（自动注入基础命名空间，用户无需手动添加）
            var usingBlock = string.Join(Environment.NewLine,
                DefaultUsings.Select(u => $"using {u};"));
            result.UsingLineOffset = DefaultUsings.Length + 1; // +1 空行
            var fullCode = usingBlock + Environment.NewLine + Environment.NewLine + code;

            // 2. 构建语法树
            var sourceText = SourceText.From(fullCode);
            var syntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText,
                new CSharpParseOptions(LanguageVersion.Latest));

            // 3. 收集引用
            var referencedAssemblies = new HashSet<Assembly>();

            foreach (var assembly in GetDefaultAssemblies())
                referencedAssemblies.Add(assembly);

            // 用户指定的 DLL 引用
            if (additionalAssemblies != null)
            {
                foreach (var dllPath in additionalAssemblies)
                {
                    if (File.Exists(dllPath))
                    {
                        try
                        {
                            var asm = Assembly.LoadFrom(dllPath);
                            referencedAssemblies.Add(asm);
                        }
                        catch (Exception ex)
                        {
                            result.Diagnostics.Add($"加载程序集引用失败: {dllPath} — {ex.Message}");
                        }
                    }
                    else
                    {
                        result.Diagnostics.Add($"程序集引用文件不存在: {dllPath}");
                    }
                }
            }

            // 4. 创建编译
            var options = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release)
                .WithAllowUnsafe(true);

            var compilation = CSharpCompilation.Create(
                assemblyName,
                [syntaxTree],
                referencedAssemblies.Select(a => MetadataReference.CreateFromFile(a.Location)),
                options);

            // 4. 编译并输出
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
        /// 从代码中提取继承 ScriptBase 的类名
        /// </summary>
        private static string? ExtractClassName(string code)
        {
            // 简单解析：查找 "class XXX : ScriptBase" 模式
            var lines = code.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Contains(": ScriptBase"))
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

        /// <summary>
        /// 获取默认引用的基础程序集
        /// </summary>
        private static List<Assembly> GetDefaultAssemblies()
        {
            var assemblies = new List<Assembly>
            {
                typeof(object).Assembly,                    // System.Private.CoreLib
                typeof(System.Linq.Enumerable).Assembly,   // System.Linq
                typeof(System.Text.StringBuilder).Assembly, // System.Text
                typeof(System.Collections.Generic.List<>).Assembly, // System.Collections
                typeof(ScriptBase).Assembly                 // 脚本引擎所在程序集
            };

            // 加载 System.Runtime（需要显式加载）
            try
            {
                var runtimeAssembly = Assembly.Load(new AssemblyName("System.Runtime"));
                if (runtimeAssembly != null)
                    assemblies.Add(runtimeAssembly);
            }
            catch
            {
                // 可选
            }

            // 加载 System.Console (有时需要)
            try
            {
                var consoleAssembly = Assembly.Load(new AssemblyName("System.Console"));
                if (consoleAssembly != null)
                    assemblies.Add(consoleAssembly);
            }
            catch
            {
                // 可选
            }

            return assemblies;
        }
    }
}
