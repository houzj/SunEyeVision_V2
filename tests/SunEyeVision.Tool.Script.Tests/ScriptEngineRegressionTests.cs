using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using SunEyeVision.TestShared;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

/// <summary>
/// 脚本引擎回归测试 — 守护"编译成功但实例化报找不到类 UserScript"的修复。
///
/// 原始 bug 根因：ScriptRefs 中的宿主 DLL 被无差别预加载到自定义 ALC，
/// 导致脚本继承的 Script 与沙箱校验用的 ScriptBase 是两份 CLR 类型副本，
/// `as ScriptBase` 静默返回 null，错误信息误导为"找不到类"。
///
/// 修复后：宿主 DLL 不再进自定义 ALC，编译元数据改从默认 ALC 已加载程序集获取，
/// ScriptRefs 清空宿主 DLL，仅承载用户自定义 DLL。
///
/// 本测试用默认模板脚本（GeneratedCode + MinimalCode）走完整 Execute 流程，
/// 断言编译+实例化成功（errors 为空），直接验证"宿主元数据来自默认 ALC、ScriptRefs 为空"的
/// 修复后状态。为此强制将宿主 DLL 加载进默认 ALC（复刻宿主应用的运行期状态）。
/// </summary>
public class ScriptEngineRegressionTests
{
    [Fact]
    public void Execute_DefaultTemplate_WithoutHostDllsInScriptRefs_InstantiatesSuccessfully()
    {
        // 宿主根 dev/Debug 由共享 Loader 的 AssemblyMetadata(HostDebugRoot) 注入提供
        // （Directory.Build.targets），不再从测试程序集位置向上探测——测试已独立输出到 bin。
        var debugDir = ToolPluginTestLoader.HostDebugRoot;

        // 复刻宿主运行期状态：宿主 DLL 已在默认 ALC 加载，脚本编译直接复用其类型元数据。
        // 缺失则明确报错（rule-008 报错优先，不静默跳过）。
        var hostDlls = new[]
        {
            "SunEyeVision.Plugin.SDK.dll",
            "SunEyeVision.CvEngine.dll",
            "SunEyeVision.Workflow.dll",
            "halcondotnetxl.dll",
        };
        foreach (var dll in hostDlls)
        {
            var path = Path.Combine(debugDir, dll);
            if (!File.Exists(path))
                throw new InvalidOperationException($"宿主 DLL 未找到（测试需先完成完整构建）: {path}");
            AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }

        var toolAsm = typeof(SunEyeVision.Tool.Script.Models.ScriptParameters).Assembly;

        // 构造默认参数：GeneratedCode 含 "UserScript : Script" 字段声明，ScriptCode 为 MinimalCode
        var paramsType = toolAsm.GetType("SunEyeVision.Tool.Script.Models.ScriptParameters")
            ?? throw new InvalidOperationException("ScriptParameters 类型未找到");
        var parameters = Activator.CreateInstance(paramsType)
            ?? throw new InvalidOperationException("ScriptParameters 实例化失败");

        var generatedCode = (string)paramsType.GetProperty("GeneratedCode")!.GetValue(parameters)!;
        var scriptCode = (string)paramsType.GetProperty("ScriptCode")!.GetValue(parameters)!;

        // 修复后状态：ScriptRefs 为空（宿主 DLL 不在此目录），使用临时空目录复刻
        var scriptRefs = Path.Combine(Path.GetTempPath(), "ScriptRefs_Empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scriptRefs);
        try
        {
            // 反射调用内部 ScriptLifecycleManager.Execute（不修改工具代码，仅测试访问）
            var mgrType = toolAsm.GetType("SunEyeVision.Tool.Script.ScriptEngine.ScriptLifecycleManager")
                ?? throw new InvalidOperationException("ScriptLifecycleManager 类型未找到");
            using var mgr = (IDisposable)Activator.CreateInstance(mgrType)!;
            var execute = mgrType.GetMethod("Execute")
                ?? throw new InvalidOperationException("Execute 方法未找到");

            List<string> errors;
            try
            {
                // Execute 第 7 参数 = 每节点引用列表（默认系统必需项），验证默认基线即可编译
                var assemblyRefs = (System.Collections.IEnumerable)paramsType.GetProperty("AssemblyReferences")!.GetValue(parameters)!;
                errors = (List<string>)execute.Invoke(mgr, new object?[]
                {
                    generatedCode, scriptCode, scriptRefs, null, null, null, assemblyRefs
                })!;
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException(
                    $"Execute 抛出异常（宿主元数据未按预期在默认 ALC 可用）: {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}",
                    ex);
            }

            Assert.True(errors.Count == 0,
                "脚本引擎执行返回错误:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
        finally
        {
            try { Directory.Delete(scriptRefs, recursive: true); } catch { /* 清理失败不影响判定 */ }
        }
    }
}
