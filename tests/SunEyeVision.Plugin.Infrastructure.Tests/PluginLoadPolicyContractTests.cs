using System;
using System.IO;
using System.Linq;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using Xunit;

namespace SunEyeVision.Plugin.Infrastructure.Tests;

/// <summary>
/// 守卫契约测试：插件加载策略（方案《工具插件自包含与独立开发方案》P1「契约与加载」）。
/// </summary>
/// <remarks>
/// 本组测试锁死两件**改坏后表面看不出来**的事：
/// <list type="bullet">
/// <item><b>契约/框架程序集必须回落宿主</b> —— 一旦改成本地加载，插件目录内的 SDK 副本会被独立载入，
///   <c>IToolPlugin</c> 出现两份同名接口 ⇒ <c>IsAssignableFrom</c> 恒 false ⇒
///   表现为「插件加载了但工具不见了」（最典型、最费时的排查场景）；</item>
/// <item><b>只把"与目录同名的主程序集"当插件</b> —— 自包含后插件目录里还有契约/框架/第三方副本，
///   若退回"扫描目录内所有 dll"，那些副本会被带入宿主（同样的类型身份隐患）。</item>
/// </list>
/// </remarks>
public class PluginLoadPolicyContractTests
{
    [Fact]
    public void IsHostProvided_Contract_Framework_AndPlatform_AreHostOwned()
    {
        // ① 契约：只留一份（类型身份的唯一来源）
        Assert.True(PluginAssemblyPolicy.IsHostProvided("SunEyeVision.Plugin.SDK"));

        // ② 框架模块与宿主自身程序集（工具目录里常有它们的副本，但运行期必须用宿主那份）
        Assert.True(PluginAssemblyPolicy.IsHostProvided("SunEyeVision.Plugin.Infrastructure"));
        Assert.True(PluginAssemblyPolicy.IsHostProvided("SunEyeVision.HalAdapter"));
        Assert.True(PluginAssemblyPolicy.IsHostProvided("SunEyeVision.Core"));

        // 注：大件平台（halcon/OpenCV）**不在此判定** —— 本类不再维护"大件平台清单"，
        // 以免与 Directory.Build.targets 的剔除清单形成两份（加一处漏一处即静默分叉）。
        // 大件改由加载期的**事实判定**覆盖：宿主已加载 / 宿主运行目录可解析 ⇒ 共用宿主那份
        //（见 PluginLoadContext.Load 的第 ③/④ 条）。清单的唯一真相源 = 构建期那一份（体积治理）。

        // ③ 平台/框架程序集：**必须**回宿主。
        //    实测事故（2026-09-14）：工具目录自带 System.Text.Json ⇒ 特性类型身份分裂 ⇒
        //    [JsonDerivedType] 扫描不到 ⇒ 加载解决方案报 "unrecognized type discriminator id 'Script'"。
        Assert.True(PluginAssemblyPolicy.IsHostProvided("System.Text.Json"));
        Assert.True(PluginAssemblyPolicy.IsHostProvided("System.Private.Windows.Core"));
        Assert.True(PluginAssemblyPolicy.IsHostProvided("PresentationFramework"));
        Assert.True(PluginAssemblyPolicy.IsHostProvided("netstandard"));
    }

    [Fact]
    public void IsHostProvided_ToolThirdParty_AndUnknown_AreNotHostOwned()
    {
        // 工具自带第三方库 → 在插件隔间内解析（"自带行李"），不得回落宿主
        Assert.False(PluginAssemblyPolicy.IsHostProvided("Newtonsoft.Json"));
        Assert.False(PluginAssemblyPolicy.IsHostProvided("MyCompany.VisionLib"));

        // 空/异常输入不得误判为宿主提供（否则会静默回落，问题被隐藏）
        Assert.False(PluginAssemblyPolicy.IsHostProvided(null));
        Assert.False(PluginAssemblyPolicy.IsHostProvided(string.Empty));
    }

    [Fact]
    public void DiscoverPluginAssemblies_TakesOnlySameNamedMainAssembly()
    {
        // 模拟自包含后的插件目录：主程序集 + 契约副本 + 第三方副本 + 一个数据目录
        var root = Path.Combine(Path.GetTempPath(), "SunEyeVisionPluginPolicy_" + Guid.NewGuid().ToString("N"));
        try
        {
            var toolDir = Path.Combine(root, "SunEyeVision.Tool.Demo");
            var dataDir = Path.Combine(root, "ScriptRefs");
            Directory.CreateDirectory(toolDir);
            Directory.CreateDirectory(dataDir);

            var mainAssembly = Path.Combine(toolDir, "SunEyeVision.Tool.Demo.dll");
            File.WriteAllText(mainAssembly, "x");
            File.WriteAllText(Path.Combine(toolDir, "SunEyeVision.Plugin.SDK.dll"), "x"); // 行李：契约副本
            File.WriteAllText(Path.Combine(toolDir, "AvalonEdit.dll"), "x");              // 行李：第三方
            File.WriteAllText(Path.Combine(dataDir, "notAPlugin.dll"), "x");              // 非插件目录

            var discovered = PluginAssemblyPolicy.DiscoverPluginAssemblies(root);

            // 只应发现"与目录同名的主程序集"：目录内的依赖副本与非插件目录一律不得被当作插件载入
            Assert.Equal(new[] { mainAssembly }, discovered.ToArray());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DiscoverPluginAssemblies_MissingDirectory_ReturnsEmpty()
    {
        // 目录不存在不是异常（可能用户没装插件）—— 由调用方按"无插件"处理并给出可见日志
        var missing = Path.Combine(Path.GetTempPath(), "SunEyeVisionMissing_" + Guid.NewGuid().ToString("N"));

        Assert.Empty(PluginAssemblyPolicy.DiscoverPluginAssemblies(missing));
    }
}
