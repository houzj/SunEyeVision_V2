using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

/// <summary>
/// halcon 宿主 API 编译契约测试（防 CS0012/CS0246 回归）。
/// 守护：含 Halcon 算子的脚本（HOperatorSet / HImage）能走 ScriptLifecycleManager.Execute
/// 完整编译 + 实例化路径，0 编译错误。
/// 历史问题：宿主 API 元数据引用缺失 mscorlib（halcondotnetxl 为 .NET Framework 时代程序集，
/// 引用 mscorlib 2.0.0.0）→ Roslyn 解析其基类/接口签名时报 CS0012 "类型 Object 在未引用的程序集中定义"。
/// 白名单补 mscorlib 后此测试应通过。
/// </summary>
public class HalconScriptCompileTests
{
    /// <summary>
    /// 复刻宿主运行期状态：宿主 DLL 已在默认 ALC 加载（与 ScriptEngineRegressionTests 一致）。
    /// 缺则明确报错，不静默（rule-008）。
    /// </summary>
    private static Assembly LoadToolAssembly()
    {
        var testAsmDir = Path.GetDirectoryName(typeof(HalconScriptCompileTests).Assembly.Location)
            ?? throw new InvalidOperationException("无法获取测试程序集路径");
        var debugDir = testAsmDir;
        while (debugDir != null && !File.Exists(Path.Combine(debugDir, "SunEyeVision.Plugin.SDK.dll")))
        {
            debugDir = Path.GetDirectoryName(debugDir);
        }
        if (debugDir == null)
            throw new InvalidOperationException("未找到宿主构建目录 dev/Debug（请先完成完整构建）");

        // 宿主 DLL 已在默认 ALC（由 ScriptEngineRegressionTests 或主程序加载），此处确保 halcon 就位
        foreach (var dll in new[] { "halcondotnetxl.dll" })
        {
            var path = Path.Combine(debugDir, dll);
            if (!File.Exists(path))
                throw new InvalidOperationException($"宿主 DLL 未找到: {path}");
            try { AssemblyLoadContext.Default.LoadFromAssemblyPath(path); }
            catch (FileLoadException) { /* 已加载则忽略 */ }
        }

        // 工具 DLL（plugins 子目录）
        var toolDll = Path.Combine(debugDir, "plugins", "SunEyeVision.Tool.Script", "SunEyeVision.Tool.Script.dll");
        if (!File.Exists(toolDll))
            throw new InvalidOperationException($"工具 DLL 未找到: {toolDll}");
        try { AssemblyLoadContext.Default.LoadFromAssemblyPath(toolDll); }
        catch (FileLoadException) { /* 已加载则忽略 */ }

        return AssemblyLoadContext.Default.Assemblies
            .First(a => a.GetName().Name == "SunEyeVision.Tool.Script");
    }

    /// <summary>
    /// 含 Halcon 算子的脚本编译 + 实例化，0 错误（防 CS0012/CS0246 回归）。
    /// 用真实 DemoCode（HOperatorSet.GetImageSize / Threshold / Connection 等）。
    /// </summary>
    [Fact]
    public void Execute_DemoWithHalconOperators_CompilesWithoutError()
    {
        var toolAsm = LoadToolAssembly();

        // 读取 DemoCode 资源作为含 halcon 算子的脚本源码
        var scriptCode = LoadResource(toolAsm, "SunEyeVision.Tool.Script.Resources.DemoCode.txt");
        Assert.Contains("HOperatorSet", scriptCode);

        // GeneratedCode 模板（用户脚本继承 Script 基类 + input/output 字段）
        var paramsType = toolAsm.GetType("SunEyeVision.Tool.Script.Models.ScriptParameters")
            ?? throw new InvalidOperationException("ScriptParameters 类型未找到");
        var parameters = Activator.CreateInstance(paramsType)
            ?? throw new InvalidOperationException("ScriptParameters 实例化失败");
        var generatedCode = (string)paramsType.GetProperty("GeneratedCode")!.GetValue(parameters)!;

        var scriptRefs = Path.Combine(Path.GetTempPath(), "ScriptRefs_Halcon_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scriptRefs);
        try
        {
            var mgrType = toolAsm.GetType("SunEyeVision.Tool.Script.ScriptEngine.ScriptLifecycleManager")
                ?? throw new InvalidOperationException("ScriptLifecycleManager 类型未找到");
            using var mgr = (IDisposable)Activator.CreateInstance(mgrType)!;
            var execute = mgrType.GetMethod("Execute")
                ?? throw new InvalidOperationException("Execute 方法未找到");

            var errors = (List<string>)execute.Invoke(mgr, new object?[]
            {
                generatedCode, scriptCode, scriptRefs, null, null, null
            })!;

            Assert.True(errors.Count == 0,
                "含 halcon 算子的脚本编译失败（可能 CS0012/CS0246 回归）:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors));
        }
        finally
        {
            try { Directory.Delete(scriptRefs, recursive: true); } catch { /* 清理失败不影响判定 */ }
        }
    }

    /// <summary>
    /// 从工具程序集读取嵌入式资源文本（DemoCode.txt）。
    /// </summary>
    private static string LoadResource(Assembly asm, string resourceName)
    {
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"嵌入式资源未找到: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
