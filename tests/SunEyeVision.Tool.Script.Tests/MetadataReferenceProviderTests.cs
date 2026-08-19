using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;

namespace SunEyeVision.Tool.Script.Tests;

/// <summary>
/// MetadataReferenceProvider 引用三层分离契约测试（UT-REF-01 ~ UT-REF-04）。
/// 守护 L1 引用白名单化的完整性：
///   - 引用总数从全量 287 降至显式核心（BCL 白名单 ~33 + 宿主 3），杜绝全量 TPA/ALC 扫描
///   - BCL 白名单覆盖脚本模板（DemoCode/GeneratedCode）用到的所有命名空间
///   - 宿主 API（Plugin.SDK / halcon / OpenCvSharp）锚定可用
///   - ScriptRefs 用户增量不受影响
/// </summary>
public class MetadataReferenceProviderTests
{
    /// <summary>
    /// 加载工具程序集（plugins 子目录）。类型锚定后，halcon/OpenCvSharp/Plugin.SDK 由
    /// 工具 DLL 的元数据依赖自动解析（测试运行目录 dev/Debug 根有这些 DLL，CLR probing 命中），
    /// 不再手动强加载——真实反映生产环境的惰性加载行为（rule-008 测试与生产一致）。
    /// </summary>
    private static void EnsureToolDllLoaded()
    {
        var testAsmDir = Path.GetDirectoryName(typeof(MetadataReferenceProviderTests).Assembly.Location)
            ?? throw new InvalidOperationException("无法获取测试程序集路径");
        var debugDir = testAsmDir;
        while (debugDir != null && !File.Exists(Path.Combine(debugDir, "SunEyeVision.Plugin.SDK.dll")))
        {
            debugDir = Path.GetDirectoryName(debugDir);
        }
        if (debugDir == null)
            throw new InvalidOperationException("未找到宿主构建目录 dev/Debug（请先完成完整构建）");

        // 工具 DLL（plugins 子目录），依赖由 CLR probing 自动解析
        var toolDll = Path.Combine(debugDir, "plugins", "SunEyeVision.Tool.Script", "SunEyeVision.Tool.Script.dll");
        if (!File.Exists(toolDll))
            throw new InvalidOperationException($"工具 DLL 未找到: {toolDll}");
        try { AssemblyLoadContext.Default.LoadFromAssemblyPath(toolDll); }
        catch (FileLoadException) { /* 已加载则忽略 */ }
    }

    private static (IReadOnlyList<object> refs, IReadOnlyList<string> paths) GetHostReferences()
    {
        EnsureToolDllLoaded();
        var toolAsm = AssemblyLoadContext.Default.Assemblies
            .First(a => a.GetName().Name == "SunEyeVision.Tool.Script");
        var type = toolAsm
            .GetType("SunEyeVision.Tool.Script.ScriptEngine.MetadataReferenceProvider")
            ?? throw new InvalidOperationException("MetadataReferenceProvider 类型未找到");
        var method = type.GetMethod("GetHostReferences", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetHostReferences 方法未找到");

        var result = (IEnumerable<object>)method.Invoke(null, null)!;
        var list = result.ToList();
        var paths = new List<string>();
        foreach (var mdRef in list)
        {
            var filePath = (string?)mdRef.GetType().GetProperty("FilePath")?.GetValue(mdRef);
            if (filePath != null) paths.Add(Path.GetFileName(filePath));
        }
        return (list, paths);
    }

    /// <summary>
    /// UT-REF-01: 引用总数显著小于全量 287，证明白名单化生效（杜绝全量 TPA/ALC 扫描）。
    /// </summary>
    [Fact]
    public void GetHostReferences_TotalCount_MuchSmallerThanFullTpa()
    {
        var (refs, _) = GetHostReferences();
        // 白名单 ~33 BCL + 3 宿主，远小于原全量 287
        Assert.InRange(refs.Count, 20, 80);
    }

    /// <summary>
    /// UT-REF-01b: 白名单覆盖模板 using 所需的核心 BCL 程序集。
    /// GeneratedCode using: System / System.Collections.Generic / System.Linq / System.Text / System.IO。
    /// </summary>
    [Fact]
    public void GetHostReferences_ContainsCoreBcl_UsedByGeneratedCode()
    {
        var (_, paths) = GetHostReferences();
        foreach (var expected in new[]
                 {
                     "System.Private.CoreLib.dll",
                     "System.Runtime.dll",
                     "System.Linq.dll",
                     "System.Collections.dll",
                     "System.IO.dll",
                     "System.Text.Encoding.dll",
                 })
        {
            Assert.Contains(paths, p => string.Equals(p, expected, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// UT-REF-02: 宿主 API 锚定 — Plugin.SDK / halcon / OpenCvSharp 均在引用中。
    /// </summary>
    [Fact]
    public void GetHostReferences_ContainsHostApi_Anchored()
    {
        var (_, paths) = GetHostReferences();
        foreach (var expected in new[] { "SunEyeVision.Plugin.SDK.dll", "halcondotnetxl.dll", "OpenCvSharp.dll" })
        {
            Assert.Contains(paths, p => string.Equals(p, expected, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// UT-REF-03: 白名单内程序集缺失时行为可见（TPA 为空 → Warning，不静默）。
    /// 无法在单测中清空 TPA，此处验证 BCL 白名单核心程序集若在 TPA 中必被引用；
    /// 缺失可见性由 AddBclWhitelist 的 Warning 日志保证（代码 review）。
    /// </summary>
    [Fact]
    public void GetHostReferences_IncludesNetStandard_ForCompat()
    {
        var (_, paths) = GetHostReferences();
        Assert.Contains(paths, p => string.Equals(p, "netstandard.dll", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// UT-REF-03b: mscorlib 必须在引用中（防 CS0012 回归）。
    /// halcondotnetxl 等 .NET Framework 时代宿主库引用 mscorlib 2.0.0.0，
    /// Roslyn 解析其基类/接口签名时需 mscorlib 引用做统一，缺失报 CS0012 "Object 未引用"。
    /// .NET 9 的 mscorlib.dll 是 facade（type-forward 到 System.Private.CoreLib）。
    /// </summary>
    [Fact]
    public void GetHostReferences_IncludesMscorlib_ForHalconCompat()
    {
        var (_, paths) = GetHostReferences();
        Assert.Contains(paths, p => string.Equals(p, "mscorlib.dll", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// UT-REF-04: 引用按路径去重（无重复导致的 CS1703）。
    /// </summary>
    [Fact]
    public void GetHostReferences_NoDuplicatePaths()
    {
        var (_, paths) = GetHostReferences();
        var unique = paths.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        Assert.Equal(paths.Count, unique);
    }
}
