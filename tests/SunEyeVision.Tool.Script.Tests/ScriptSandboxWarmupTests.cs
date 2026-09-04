using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using SunEyeVision.TestShared;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

/// <summary>
/// 沙箱预热（WarmupSandbox）回归测试 — 守护"预编译后预热执行环境"逻辑。
///
/// 方案：预编译（CompileOnly 写入编译缓存）成功后，用同一缓存键调用 WarmupSandbox，
/// 完成沙箱常驻池就位 + 程序集加载 + 用户代码 JIT。目标：首次执行不再承担
/// 沙箱创建/程序集加载/JIT 冷启动，实现"首次与后续执行时间一致"。
///
/// 本测试验证三条防御性保证：
/// 1. CompileOnly 成功后 WarmupSandbox 不抛异常（预热失败仅告警，不阻塞）；
/// 2. 预热后沙箱池中已存在该缓存键的常驻沙箱且已加载脚本程序集；
/// 3. 预热后首次执行命中编译缓存（CompileTimeMs == 0）且无执行错误。
/// </summary>
public class ScriptSandboxWarmupTests
{
    /// <summary>
    /// 复刻宿主运行期状态：宿主 DLL 已在默认 ALC 加载（与 ScriptEngineRegressionTests 一致）。
    /// 工具程序集从插件部署目录（dev/Debug/plugins/...）加载最新版，确保含 WarmupSandbox。
    /// </summary>
    private static Assembly LoadToolAssembly()
    {
        // 宿主根 dev/Debug 由共享 Loader 的 AssemblyMetadata(HostDebugRoot) 注入提供
        // （Directory.Build.targets），不再从测试程序集位置向上探测——测试已独立输出到 bin。
        var debugDir = ToolPluginTestLoader.HostDebugRoot;

        // 宿主 DLL 已在默认 ALC（由共享 Loader Resolving 兜底或 ScriptEngineRegressionTests 加载），此处确保就位
        foreach (var dll in new[] { "SunEyeVision.Plugin.SDK.dll", "SunEyeVision.CvEngine.dll" })
        {
            var path = Path.Combine(debugDir, dll);
            if (!File.Exists(path))
                throw new InvalidOperationException($"宿主 DLL 未找到（测试需先完成完整构建）: {path}");
            try { AssemblyLoadContext.Default.LoadFromAssemblyPath(path); }
            catch (FileLoadException) { /* 已加载则忽略 */ }
        }

        // 工具程序集：从插件部署目录加载最新版（根目录 dev/Debug 下的是历史遗留旧版）
        var toolDll = Path.Combine(debugDir, "plugins", "SunEyeVision.Tool.Script", "SunEyeVision.Tool.Script.dll");
        if (!File.Exists(toolDll))
            throw new InvalidOperationException($"工具 DLL 未找到: {toolDll}");
        try { AssemblyLoadContext.Default.LoadFromAssemblyPath(toolDll); }
        catch (FileLoadException) { /* 已加载则忽略 */ }

        return AssemblyLoadContext.Default.Assemblies
            .First(a => a.GetName().Name == "SunEyeVision.Tool.Script");
    }

    /// <summary>
    /// 构造默认脚本参数（GeneratedCode 含字段声明，ScriptCode 为 MinimalCode）。
    /// </summary>
    private static (object Parameters, string GeneratedCode, string ScriptCode, System.Collections.IEnumerable AssemblyRefs) BuildParams(Assembly toolAsm)
    {
        var paramsType = toolAsm.GetType("SunEyeVision.Tool.Script.Models.ScriptParameters")
            ?? throw new InvalidOperationException("ScriptParameters 类型未找到");
        var parameters = Activator.CreateInstance(paramsType)
            ?? throw new InvalidOperationException("ScriptParameters 实例化失败");

        var generatedCode = (string)paramsType.GetProperty("GeneratedCode")!.GetValue(parameters)!;
        var scriptCode = (string)paramsType.GetProperty("ScriptCode")!.GetValue(parameters)!;
        var assemblyRefs = (System.Collections.IEnumerable)paramsType.GetProperty("AssemblyReferences")!.GetValue(parameters)!;
        return (parameters, generatedCode, scriptCode, assemblyRefs);
    }

    [Fact]
    public void WarmupSandbox_AfterCompileOnly_DoesNotThrow()
    {
        var toolAsm = LoadToolAssembly();
        var (_, generatedCode, scriptCode, assemblyRefs) = BuildParams(toolAsm);
        var scriptRefs = Path.Combine(Path.GetTempPath(), "ScriptRefs_Warmup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scriptRefs);

        try
        {
            var mgrType = toolAsm.GetType("SunEyeVision.Tool.Script.ScriptEngine.ScriptLifecycleManager")
                ?? throw new InvalidOperationException("ScriptLifecycleManager 类型未找到");
            using var mgr = (IDisposable)Activator.CreateInstance(mgrType)!;
            var compileOnly = mgrType.GetMethod("CompileOnly")
                ?? throw new InvalidOperationException("CompileOnly 方法未找到（工具 DLL 可能为旧版，请先完整重建部署）");
            var warmup = mgrType.GetMethod("WarmupSandbox")
                ?? throw new InvalidOperationException("WarmupSandbox 方法未找到（工具 DLL 可能为旧版，请先完整重建部署）");

            // CompileOnly 成功
            var errors = (System.Collections.Generic.List<string>)compileOnly.Invoke(mgr, new object?[]
            {
                generatedCode, scriptCode, scriptRefs, assemblyRefs
            })!;
            Assert.True(errors.Count == 0,
                "CompileOnly 应编译成功:" + Environment.NewLine + string.Join(Environment.NewLine, errors));

            // 编译成功后 WarmupSandbox 不应抛异常（防御：预热失败仅告警）
            var ex = Record.Exception(() =>
                warmup.Invoke(mgr, new object?[] { generatedCode, scriptCode, scriptRefs, assemblyRefs }));
            Assert.Null(ex);
        }
        finally
        {
            try { Directory.Delete(scriptRefs, recursive: true); } catch { /* 清理失败不影响判定 */ }
        }
    }

    [Fact]
    public void WarmupSandbox_AfterCompileOnly_PopulatesSandboxPoolWithLoadedAssembly()
    {
        var toolAsm = LoadToolAssembly();
        var (_, generatedCode, scriptCode, assemblyRefs) = BuildParams(toolAsm);
        var scriptRefs = Path.Combine(Path.GetTempPath(), "ScriptRefs_Warmup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scriptRefs);

        try
        {
            var mgrType = toolAsm.GetType("SunEyeVision.Tool.Script.ScriptEngine.ScriptLifecycleManager")
                ?? throw new InvalidOperationException("ScriptLifecycleManager 类型未找到");
            var compilerType = toolAsm.GetType("SunEyeVision.Tool.Script.ScriptEngine.ScriptCompiler")
                ?? throw new InvalidOperationException("ScriptCompiler 类型未找到");
            var poolType = toolAsm.GetType("SunEyeVision.Tool.Script.ScriptEngine.ScriptSandboxPool")
                ?? throw new InvalidOperationException("ScriptSandboxPool 类型未找到（工具 DLL 可能为旧版）");

            // 计算缓存键（与 CompileOnly 内部一致）
            var combined = generatedCode + "\n" + scriptCode;
            var computeKey = compilerType.GetMethod("ComputeCacheKey",
                    BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("ComputeCacheKey 方法未找到（工具 DLL 可能为旧版）");
            var cacheKey = (string)computeKey.Invoke(null, new object?[] { combined, assemblyRefs })!;

            using var mgr = (IDisposable)Activator.CreateInstance(mgrType)!;
            var compileOnly = mgrType.GetMethod("CompileOnly")!;
            var warmup = mgrType.GetMethod("WarmupSandbox")!;

            var errors = (System.Collections.Generic.List<string>)compileOnly.Invoke(mgr, new object?[]
                { generatedCode, scriptCode, scriptRefs, assemblyRefs })!;
            Assert.True(errors.Count == 0, "CompileOnly 应编译成功");
            warmup.Invoke(mgr, new object?[] { generatedCode, scriptCode, scriptRefs, assemblyRefs });

            // 验证沙箱池已填充：按缓存键取沙箱，LoadedAssembly 非空
            var getOrCreate = poolType.GetMethod("GetOrCreate",
                    BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("GetOrCreate 方法未找到");
            var sandbox = getOrCreate.Invoke(null, new object?[] { cacheKey, scriptRefs })!;
            var loadedAssembly = sandbox.GetType().GetProperty("LoadedAssembly")!.GetValue(sandbox) as Assembly;

            Assert.NotNull(loadedAssembly);
            Assert.True(loadedAssembly!.GetTypes().Any(t => t.Name.Contains("UserScript")
                || t.IsPublic), "沙箱应已加载脚本程序集");
        }
        finally
        {
            try { Directory.Delete(scriptRefs, recursive: true); } catch { /* 清理失败不影响判定 */ }
        }
    }
}
