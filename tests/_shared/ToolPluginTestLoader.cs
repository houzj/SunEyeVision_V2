using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace SunEyeVision.TestShared;

/// <summary>
/// 工具插件测试共享加载器（rule-016 去重 / 根治 "dev 根 vs plugins 双份 dll" / 测试输出独立化）。
///
/// 背景：工具项目 IsToolPlugin=true，真实产物唯一部署在 dev/Debug/plugins/&lt;ToolName&gt;/，
///       主程序运行时 PluginManager.LoadPlugins 从该目录 Assembly.LoadFrom（见 UI/App.xaml.cs）。
///       测试项目强引用工具类型，但工具 OutDir 被重定向到 plugins 子目录，
///       且测试项目对工具的 ProjectReference 已设 &lt;Private&gt;false&lt;/Private&gt;（不 copy-local），
///       因此测试运行目录不存在工具 dll —— 必须从 plugins 显式加载最新部署版。
///
/// 测试输出独立化（2026-09）：测试项目(IsTestProject)回落到各自 bin\&lt;Config&gt;\&lt;TFM&gt;\ 输出，
///   不再混入 dev/Debug。宿主根 dev/Debug 经 Directory.Build.targets 注入为程序集
///   AssemblyMetadata("HostDebugRoot")，本 Loader 运行时读取（不再依赖向上探测 SDK.dll，
///   因 bin 内可能 copy-local 了 SDK.dll 会误判）。
///
/// 职责：
///   1. ModuleInitializer（先于任何测试方法 JIT）注册默认 ALC 的 Resolving 兜底：
///      当 bin 内解析不到某宿主程序集（如 halcondotnetxl / 主程序特供 dll）时，
///      从宿主根 dev/Debug 加载同名 dll —— 与主程序"宿主统一加载"模型一致（rule-008）。
///   2. 按"测试程序集名去掉 .Tests 后缀 = 工具名"约定，从宿主根 plugins/&lt;Tool&gt;\
///      LoadFromAssemblyPath 加载最新工具 dll 到默认 ALC，使后续工具类型解析命中已加载程序集。
///   3. 若默认 ALC 已存在来自非 plugins 路径的同名工具程序集（历史残留旧拷贝），
///      直接报错并提示清理（rule-008 报错优先，不静默回退到旧版）。
///
/// 接入方式：测试 csproj 加一行
///   &lt;Compile Include="..\_shared\ToolPluginTestLoader.cs" Link="_shared\ToolPluginTestLoader.cs" /&gt;
/// 程序集名约定：测试程序集 = "SunEyeVision.Tool.&lt;Tool&gt;.Tests" → 工具 = "SunEyeVision.Tool.&lt;Tool&gt;"。
/// </summary>
internal static class ToolPluginTestLoader
{
    private const string HostDebugRootKey = "HostDebugRoot";
    private const string HostMarkerDll = "SunEyeVision.Plugin.SDK.dll";
    private const string TestSuffix = ".Tests";
    private const string ToolPrefix = "SunEyeVision.Tool.";

    private static string? _hostDebugRoot;
    private static bool _resolvingRegistered;

    /// <summary>宿主根绝对路径（dev/Debug）。优先读 AssemblyMetadata("HostDebugRoot")（Directory.Build.targets 注入）。</summary>
    internal static string HostDebugRoot
    {
        get
        {
            if (_hostDebugRoot == null)
            {
                var metadata = (typeof(ToolPluginTestInitializer).Assembly
                                    .GetCustomAttributes(typeof(AssemblyMetadataAttribute), inherit: false)
                                    .Cast<AssemblyMetadataAttribute>()
                                    .FirstOrDefault(a => a.Key == HostDebugRootKey))
                               ?.Value;
                _hostDebugRoot = string.IsNullOrWhiteSpace(metadata)
                    ? LocateDebugRootByProbing() // 回退：旧布局无 metadata 时向上探测
                    : metadata;
            }
            return _hostDebugRoot;
        }
    }

    /// <summary>
    /// 由 ModuleInitializer 调用：注册宿主 Resolving 兜底，并按约定加载本测试对应的工具插件。
    /// 非 "SunEyeVision.Tool.*.Tests" 命名的程序集（如框架/核心测试）仅注册 Resolving，跳过工具加载。
    /// </summary>
    internal static void EnsureToolPluginLoadedFor(Assembly testAssembly)
    {
        EnsureHostResolvingRegistered();

        var testAsmName = testAssembly.GetName().Name ?? string.Empty;
        if (!testAsmName.EndsWith(TestSuffix, StringComparison.Ordinal)) return;
        var toolName = testAsmName[..^TestSuffix.Length];
        if (!toolName.StartsWith(ToolPrefix, StringComparison.Ordinal)) return;
        EnsureLoaded(toolName);
    }

    /// <summary>幂等加载 plugins/&lt;toolName&gt;/&lt;toolName&gt;.dll 到默认 ALC；已加载则返回现有实例。</summary>
    internal static Assembly EnsureLoaded(string toolAssemblyName)
    {
        var already = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(a => a.GetName().Name == toolAssemblyName);
        if (already != null)
        {
            var loc = already.Location;
            var expectedSeg = $"{Path.DirectorySeparatorChar}plugins{Path.DirectorySeparatorChar}{toolAssemblyName}{Path.DirectorySeparatorChar}";
            if (loc.IndexOf(expectedSeg, StringComparison.OrdinalIgnoreCase) >= 0)
                return already; // 已是 plugins 部署版

            throw new InvalidOperationException(
                $"工具程序集 {toolAssemblyName} 已从非 plugins 路径加载: {loc}。" +
                $"工具 dll 唯一部署在 dev/Debug/plugins/{toolAssemblyName}/，" +
                $"请删除 dev/Debug 根目录的遗留 {toolAssemblyName}.dll 后重跑测试。");
        }

        var toolDll = Path.Combine(HostDebugRoot, "plugins", toolAssemblyName, toolAssemblyName + ".dll");
        if (!File.Exists(toolDll))
            throw new InvalidOperationException($"工具 DLL 未找到（请先完整构建工具项目）: {toolDll}");
        return AssemblyLoadContext.Default.LoadFromAssemblyPath(toolDll);
    }

    /// <summary>
    /// 注册默认 ALC 的 Resolving 兜底：bin 内解析失败时尝试从宿主根 dev/Debug 加载同名 dll。
    /// 只处理宿主特供 dll（halcondotnetxl 等主程序统一加载项），幂等，全局只注册一次。
    /// </summary>
    private static void EnsureHostResolvingRegistered()
    {
        if (_resolvingRegistered) return;
        _resolvingRegistered = true;

        AssemblyLoadContext.Default.Resolving += (_, args) =>
        {
            var name = args.Name;
            if (string.IsNullOrEmpty(name)) return null;

            // 已在默认 ALC 加载的（含 bin copy-local 命中）不需要兜底
            var loaded = AssemblyLoadContext.Default.Assemblies
                .FirstOrDefault(a => a.GetName().Name == name);
            if (loaded != null) return loaded;

            // 宿主根 dll 存在才加载；不存在保持 null（由 CLR 抛标准 FileNotFound，报错可见）
            var hostDll = Path.Combine(HostDebugRoot, name + ".dll");
            return File.Exists(hostDll)
                ? AssemblyLoadContext.Default.LoadFromAssemblyPath(hostDll)
                : null;
        };
    }

    /// <summary>回退定位：从测试运行目录向上找含 SunEyeVision.Plugin.SDK.dll 的目录（旧布局，无 AssemblyMetadata 时）。</summary>
    private static string LocateDebugRootByProbing()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, HostMarkerDll)))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException(
                $"未找到宿主构建目录 dev/Debug（缺 {HostMarkerDll}，请先完成完整构建）");
    }
}

/// <summary>
/// 程序集加载期初始化：测试程序集被 CLR 加载后、任何测试方法执行前，注册宿主 Resolving
/// 并从 plugins 加载工具 dll。本文件经 &lt;Compile Include&gt; 链接进各测试项目，从而每个测试程序集自带初始化逻辑。
/// </summary>
internal static class ToolPluginTestInitializer
{
    [ModuleInitializer]
    internal static void Init() =>
        ToolPluginTestLoader.EnsureToolPluginLoadedFor(typeof(ToolPluginTestInitializer).Assembly);
}
