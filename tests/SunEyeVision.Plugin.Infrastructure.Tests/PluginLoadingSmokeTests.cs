using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using Xunit;
using Xunit.Abstractions;

namespace SunEyeVision.Plugin.Infrastructure.Tests;

/// <summary>
/// 端到端冒烟：用**真实构建产物**的 <c>dev/Debug/plugins</c> 跑一遍插件加载，输出逐个插件的结果。
/// </summary>
/// <remarks>
/// <b>为什么需要</b>：插件加载发生在 <c>App.xaml.cs</c> 极早期，而文件日志写入器是**之后**才挂载的
/// ⇒ 主程序日志里看不到"哪个插件被拒、为什么"。诊断"工具不见了"这类问题时，
/// 只有这段冒烟能给出**逐插件的可见原因**（方案 §十一 防御机制 #3/#4）。
///
/// <b>为什么是"按需运行"</b>：它会向宿主进程注册真实工具（污染 <see cref="ToolRegistry"/> 的静态状态），
/// 与其他契约测试并行会互相干扰；且依赖构建产物（仓外/未构建环境无意义）。
/// 故默认跳过，设 <c>SUNEYE_PLUGIN_SMOKE=1</c> 时执行 —— 对应方案 §十二 验收 4「拷进即用」的自助化版本。
/// 运行：<c>$env:SUNEYE_PLUGIN_SMOKE=1; dotnet test tests\SunEyeVision.Plugin.Infrastructure.Tests --filter "FullyQualifiedName~PluginLoadingSmokeTests" --logger "console;verbosity=detailed"</c>
/// </remarks>
public class PluginLoadingSmokeTests
{
    private readonly ITestOutputHelper _output;

    public PluginLoadingSmokeTests(ITestOutputHelper output) => _output = output;

    /// <summary>把加载日志收进内存，供测试输出（避免依赖文件日志）。</summary>
    private sealed class CapturingLogger : ILogger
    {
        private readonly List<string> _lines;

        public CapturingLogger(List<string> lines) => _lines = lines;

        public void Log(LogLevel level, string message, string? source = null,
            Exception? exception = null, LogTarget target = LogTarget.All)
            => _lines.Add($"{(exception == null ? "" : "!")}{level} {message}");

        /// <summary>本冒烟不需要分来源，直接返回自身。</summary>
        public ILogger ForSource(string source) => this;
    }

    [Fact]
    public void LoadPlugins_RealBuildOutput_ReportsPerPluginOutcome()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SUNEYE_PLUGIN_SMOKE"), "1", StringComparison.Ordinal))
            return; // 按需运行（见类注释）

        var pluginDirectory = FindPluginDirectory();
        if (pluginDirectory == null)
            return; // 未构建/仓外环境：不做断言

        _output.WriteLine($"插件目录: {pluginDirectory}");

        var lines = new List<string>();
        var manager = new PluginManager(new CapturingLogger(lines));
        manager.LoadPlugins(pluginDirectory);

        foreach (var line in lines)
            _output.WriteLine(line);

        var discoveredCount = PluginAssemblyPolicy.DiscoverPluginAssemblies(pluginDirectory).Count;
        _output.WriteLine($"已注册工具数: {ToolRegistry.GetToolCount()}（发现插件 {discoveredCount} 个）");

        // 冒烟判据 1「拷进即用」：发现的插件**全部**注册成功。
        // 少一个 ⇒ 工具箱少一个工具 + 该工具的参数类型未注册（后续加载含它的解决方案会失败）
        Assert.Equal(discoveredCount, ToolRegistry.GetToolCount());

        // 取证：脚本参数类型上的 [JsonDerivedType] 特性来自**哪个程序集**（判别符注册的读取依据）
        DumpScriptParameterAttributeIdentity();

        // ★ 复现现场：加载插件后，"脚本"参数的多态判别符必须可解析
        //   （现场报错：Read unrecognized type discriminator id 'Script' —— 解决方案加载失败的直接原因）
        var discriminatorException = Record.Exception(() =>
            JsonSerializer.Deserialize<ToolParameters>("{\"$type\":\"Script\"}",
                ParameterTypeRegistry.SerializationOptions));

        _output.WriteLine(discriminatorException == null
            ? "判别符 'Script' 解析成功"
            : $"判别符 'Script' 解析失败: {discriminatorException.GetType().Name} - {discriminatorException.Message}");

        Assert.Null(discriminatorException);
    }

    /// <summary>
    /// 取证：打印「脚本参数」上的 <c>[JsonDerivedType]</c> 特性与**宿主同名特性**的程序集身份，
    /// 用于判定"特性类型身份分裂 ⇒ 判别符读不到"这一假设是否成立。
    /// </summary>
    private void DumpScriptParameterAttributeIdentity()
    {
        var scriptAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "SunEyeVision.Tool.Script");
        if (scriptAssembly == null)
        {
            _output.WriteLine("[取证] 未找到已加载的 SunEyeVision.Tool.Script");
            return;
        }

        var parametersType = scriptAssembly.GetTypes().FirstOrDefault(t => t.Name.EndsWith("ScriptParameters"));
        if (parametersType == null)
        {
            _output.WriteLine("[取证] 未找到 ScriptParameters 类型");
            return;
        }

        _output.WriteLine($"[取证] {parametersType.FullName} : base={parametersType.BaseType?.FullName}");
        _output.WriteLine($"[取证]   基类可赋值给宿主 ToolParameters = {typeof(ToolParameters).IsAssignableFrom(parametersType)}");
        _output.WriteLine($"[取证]   宿主 JsonDerivedTypeAttribute 来自 {typeof(System.Text.Json.Serialization.JsonDerivedTypeAttribute).Assembly.GetName().Name}"
            + $" {typeof(System.Text.Json.Serialization.JsonDerivedTypeAttribute).Assembly.GetName().Version}");

        foreach (var attribute in parametersType.GetCustomAttributes(inherit: false))
        {
            var attributeAssembly = attribute.GetType().Assembly.GetName();
            _output.WriteLine($"[取证]   特性 {attribute.GetType().FullName} @ {attributeAssembly.Name} {attributeAssembly.Version}");
        }
    }

    /// <summary>测试产物的文件名特征（守卫用）。</summary>
    private static readonly System.Text.RegularExpressions.Regex TestArtifactPattern =
        new System.Text.RegularExpressions.Regex(
            "^(xunit|testhost|Microsoft\\.TestPlatform|Microsoft\\.VisualStudio\\.TestPlatform|Microsoft\\.Testing)"
            + "|\\.msCoverageSourceRootsMapping_|\\.TestAdapter\\.dll$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// 守卫：**宿主运行目录不得混入测试产物**（工作项 #13 / #16）。
    /// </summary>
    /// <remarks>
    /// <b>为什么需要</b>：曾实测到两类孤儿落在主程序运行目录（`dev\Debug`）：
    /// ① 测试框架（`xunit.*` / `Microsoft.TestPlatform.*` / `testhost.*`，2025 年遗留 **95 个文件 / 4.39 MB**，含 13 种语言资源）；
    /// ② 测试覆盖率映射（`.msCoverageSourceRootsMapping_*.Tests`，**16 个**）。
    /// 二者均由测试过程产生，却违反"测试项目独立完整环境、不污染主程序"的约定 ——
    /// 测试输出应一律落在 `tests\bin\&lt;配置&gt;\`。
    /// <para>未构建 / 仓外环境（找不到宿主目录）时自动跳过，不产生误报。</para>
    /// </remarks>
    [Fact]
    public void HostRunDirectory_ContainsNoTestArtifacts()
    {
        var pluginDirectory = FindPluginDirectory();
        if (pluginDirectory == null)
            return; // 未构建 / 仓外环境：跳过

        var hostDirectory = Directory.GetParent(pluginDirectory)?.FullName;
        if (hostDirectory == null)
            return;

        var leftovers = Directory.EnumerateFiles(hostDirectory, "*", SearchOption.AllDirectories)
            .Where(path => TestArtifactPattern.IsMatch(Path.GetFileName(path)))
            .ToList();

        Assert.True(leftovers.Count == 0,
            $"宿主运行目录混入 {leftovers.Count} 个测试产物：{hostDirectory}\n"
            + string.Join("\n", leftovers.Take(10))
            + "\n（测试输出应落在 tests\\bin\\<配置>\\，不得写入主程序运行目录）");
    }

    /// <summary>自测试程序集目录向上找到仓库根下的 <c>dev/Debug/plugins</c>。</summary>
    private static string? FindPluginDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "dev", "Debug", "plugins");
            if (Directory.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        return null;
    }
}
