using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin
{
    /// <summary>
    /// 插件程序集策略 —— 「哪些 dll 是插件」与「哪些交给宿主」的**唯一判定源**。
    /// </summary>
    /// <remarks>
    /// <b>为什么需要</b>（方案《工具插件自包含与独立开发方案》§六 依赖三分类）：
    /// 工具目录现在是**自包含**的（契约 / 框架模块 / 业务依赖都在目录内），目的是让工具在没有主程序
    /// 源码的机器上也能独立开发、让 XAML 设计器能解析。但「目录里有」≠「运行期要用目录里那份」：
    /// <list type="bullet">
    /// <item><b>契约与框架模块全平台只能有一份</b>（宿主那份）—— 否则同一类型来自两个程序集，
    ///   表现为「程序不报错、功能悄悄失灵」（<c>ScriptSandbox.cs:15-21</c> 已记录该坑）；</item>
    /// <item><b>大件平台</b>（Halcon / OpenCV）体积大，由宿主统一提供；</item>
    /// <item><b>工具自身与其第三方依赖</b>才在插件目录内解析（「自带行李」）。</item>
    /// </list>
    /// 判定集中在本类：新增平台级大件或调整回落范围时**只改这里**（与决策 17 同族 —— 唯一产出点）。
    /// </remarks>
    internal static class PluginAssemblyPolicy
    {
        /// <summary>
        /// <summary>框架程序集前缀：一律回落宿主（含 Plugin.SDK / Core / HalAdapter / CvAdapter 等）。</summary>
        private const string FrameworkAssemblyPrefix = "SunEyeVision.";

        /// <summary>
        /// 平台/框架程序集前缀（BCL 与共享框架）：**必须**回落宿主。
        /// </summary>
        /// <remarks>
        /// <b>实测教训（2026-09-14，事故驱动）</b>：工具目录若自带 <c>System.Text.Json.dll</c>，
        /// 隔间会优先载入它 ⇒ 该程序集的类型身份与宿主那份**分裂**。而工具参数类的
        /// <c>[JsonDerivedType]</c> 特性正是 <c>System.Text.Json</c> 的类型 ——
        /// 宿主扫描特性时用的是自己那份，于是**扫不到任何判别符**，
        /// 表现为加载解决方案时报 <c>Read unrecognized type discriminator id 'Script'</c>。
        /// 该事故只在 Script 工具复现（只有它的目录自带 System.Text.Json），但成因是**通用**的：
        /// 凡"跨宿主/插件边界的类型"（JSON 契约、日志、几何类型…），其程序集必须单一来源。
        /// </remarks>
        private static readonly string[] HostProvidedFrameworkPrefixes =
        {
            "System.",
            "Microsoft.Win32.",
            "Microsoft.VisualBasic.",
            "Microsoft.CSharp",
            "WindowsBase",
            "PresentationCore",
            "PresentationFramework",
            "Accessibility",
            "UIAutomation",
            "netstandard",
            "mscorlib",
        };

        /// <summary>
        /// 判定该程序集是否由**宿主**提供（插件目录内即使存在同名副本也不使用）。
        /// </summary>
        /// <param name="simpleName">程序集简单名（可为 <c>null</c>）</param>
        internal static bool IsHostProvided(string? simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
                return false;

            // ① 契约程序集：只留一份。这是插件能被识别为 IToolPlugin 的前提
            //    （接口类型身份必须与宿主一致，否则 IsAssignableFrom 恒 false）。
            if (string.Equals(simpleName, SunEyeVision.Plugin.SDK.Core.ToolContract.AssemblyName, StringComparison.Ordinal))
                return true;

            // ② 框架模块与宿主自身程序集：用前缀而非白名单 —— 新增框架模块无需改这里；
            //    而「工具引用了宿主专属程序集」本应属分层违规，由编译期暴露（rule-008 报错优先）。
            if (simpleName.StartsWith(FrameworkAssemblyPrefix, StringComparison.Ordinal))
                return true;

            // ③ 平台/框架程序集（BCL 与共享框架）：插件目录即使自带同名副本也必须回宿主，
            //    否则"跨边界类型"（如 [JsonDerivedType] 特性所在的 System.Text.Json）身份分裂。
            //
            // ★ 本类**不再维护"大件平台清单"**（历史上曾在此列 halcon/OpenCV，与
            //   Directory.Build.targets 的剔除清单形成**两份**，加一处漏一处即静默分叉）。
            //   大件由加载期的**事实判定**覆盖：宿主已加载、或宿主运行目录可解析 ⇒ 共用宿主那份
            //   （见 PluginLoadContext.Load 的 ③/④）。清单的唯一真相源因此只剩**构建期一份**
            //   （职责 = 体积治理：不把大件复制进工具目录），两侧再无分叉可能。
            return HostProvidedFrameworkPrefixes.Any(p => simpleName.StartsWith(p, StringComparison.Ordinal));
        }

        /// <summary>
        /// 发现插件**主程序集**：<c>plugins/&lt;Name&gt;/&lt;Name&gt;.dll</c>（drop-in 约定）
        /// 以及 <c>plugins/*.dll</c>（历史形态）。
        /// </summary>
        /// <remarks>
        /// ★ 只认「与所在目录同名的主程序集」，**不再扫描目录内所有 dll**。
        /// 自包含改造后，插件目录里还有契约 / 框架 / 第三方副本，逐个载入会把它们也载进宿主
        /// —— 契约副本一旦被载入即埋下「两套同名类型」的隐患。按约定命名可可靠区分「主程序集」与「行李」。
        /// </remarks>
        /// <param name="pluginDirectory">插件根目录（<c>plugins/</c>）</param>
        /// <returns>插件主程序集绝对路径列表（目录不存在时为空列表）</returns>
        internal static IReadOnlyList<string> DiscoverPluginAssemblies(string pluginDirectory)
        {
            var result = new List<string>();

            if (!Directory.Exists(pluginDirectory))
                return result;

            // 历史形态：直接放在 plugins 根层的 dll
            result.AddRange(Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly));

            // drop-in 约定：每个子目录 = 一个插件，主程序集与目录同名
            foreach (var subDirectory in Directory.GetDirectories(pluginDirectory))
            {
                var mainAssemblyPath = Path.Combine(subDirectory, Path.GetFileName(subDirectory) + ".dll");
                if (File.Exists(mainAssemblyPath))
                    result.Add(mainAssemblyPath);
            }

            return result;
        }
    }
}
