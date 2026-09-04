using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using HalconDotNet;
using Microsoft.CodeAnalysis;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 统一元数据引用服务 — 为 Roslyn Workspace（UI 高亮）和 CSharpCompilation（编译 Emit）提供类型元数据。
    ///
    /// 引用三层分离（对齐 RoslynPad 引用架构，杜绝全量 TPA 扫描）：
    ///   1. BCL 核心 — 按程序集名白名单精确匹配 TRUSTED_PLATFORM_ASSEMBLIES（~23 个），
    ///      覆盖完整 BCL 常用面（System.* / System.Linq / System.IO / System.Text.* / System.Net.Http 等）。
    ///   2. 宿主 API — 纯类型锚定（typeof(X).Assembly 反查程序集路径）：Plugin.SDK 的 Script /
    ///      halcondotnetxl 的 HOperatorSet / OpenCvSharp 的 Mat。程序集路径由 csproj 显式声明
    ///      （编译期确定，不依赖运行时 ALC 加载状态），保证脚本继承的 Script 基类类型身份与运行时一致。
    ///   3. 用户增量 — ScriptRefs 目录（SharedMetadataReferenceProvider 管理），本类不涉及。
    ///
    /// 所有引用 MetadataReference.CreateFromFile 惰性创建（只记路径不读内容），
    /// 编译时才真正读元数据，杜绝逐文件 PE 头校验（AssemblyName.GetAssemblyName）的 IO 开销。
    ///
    /// 不再扫描 .NET SDK RefPack 目录：部署环境（无 SDK）下 RefPack 不存在，且曾错误排除
    /// System.Private.CoreLib 导致预定义类型缺失（CS0518）。元数据统一来自运行时 TPA + 类型锚定。
    /// </summary>
    internal static class MetadataReferenceProvider
    {
        /// <summary>
        /// BCL 白名单 — 按程序集名精确匹配 TPA，覆盖脚本常用完整 BCL 面。
        /// A 必备 + B 通用编程 + C 视觉业务对接。
        /// </summary>
        private static readonly string[] CoreBclAssemblyNames =
        [
            // A 必备（编译器/运行时依赖）
            "System.Private.CoreLib",
            "System.Runtime",
            "netstandard",
            // mscorlib：.NET Framework 时代程序集（halcondotnetxl 等宿主库）的 type-forward 目标。
            // halcon 元数据引用 mscorlib 2.0.0.0，Roslyn 解析其基类/接口签名时需 mscorlib 引用做统一，
            // 缺失则报 CS0012 "类型 Object 在未引用的程序集中定义"。.NET 9 的 mscorlib.dll 是 facade，
            // type-forward 到 System.Private.CoreLib。（RoslynPad 老版白名单同样包含 mscorlib）
            "mscorlib",
            // B 通用 C# 编程
            "System.Collections",
            "System.Collections.Concurrent",
            "System.Collections.Immutable",
            "System.Linq",
            "System.Linq.Expressions",
            "System.IO",
            "System.IO.FileSystem",
            "System.IO.Compression",
            "System.Text.Encoding",
            "System.Text.RegularExpressions",
            "System.Text.Json",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Threading.Thread",
            "System.Reflection",
            "System.Runtime.Loader",
            "System.Diagnostics",
            "System.Diagnostics.Process",
            "System.Globalization",
            "System.ComponentModel",
            "System.ObjectModel",
            "System.Numerics",
            "System.Runtime.InteropServices",
            // C 视觉业务对接
            "System.Net.Http",
            "System.Net.Primitives",
            "System.Xml.ReaderWriter",
            "System.Xml.XDocument",
            "System.Drawing.Common",
            "System.Data.Common",
        ];

        private static readonly object _lock = new();
        private static ImmutableList<MetadataReference>? _cachedReferences;

        /// <summary>
        /// 获取脚本编译所需的全部宿主元数据引用（BCL + 宿主 API），按文件路径去重，进程内缓存一次。
        /// </summary>
        public static ImmutableList<MetadataReference> GetHostReferences()
        {
            EnsureLoaded();
            return _cachedReferences!;
        }

        /// <summary>
        /// 清除缓存，下次调用 EnsureLoaded 时重新加载。
        /// </summary>
        public static void InvalidateCache()
        {
            lock (_lock)
            {
                _cachedReferences = null;
            }
        }

        private static void EnsureLoaded()
        {
            if (_cachedReferences != null) return;
            lock (_lock)
            {
                if (_cachedReferences != null) return;

                var sw = System.Diagnostics.Stopwatch.StartNew();

                // 按规范化路径去重，避免重复引用导致 Roslyn CS1703
                var refs = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
                int added = 0;

                // 1. BCL 白名单：从 TPA 按名精确匹配（杜绝全量扫描）
                added += AddBclWhitelist(refs);

                // 2. 宿主 API：类型锚定（不依赖 ALC 全量扫描）
                added += AddHostApiAnchored(refs);

                _cachedReferences = refs.Values.ToImmutableList();
                sw.Stop();

                PluginLogger.Info(
                    $"[MetadataRef] 统一引用初始化完成: 总数={_cachedReferences.Count}, 加入={added}, 耗时={sw.ElapsedMilliseconds}ms",
                    nameof(MetadataReferenceProvider));
            }
        }

        /// <summary>
        /// 从运行时可信平台程序集（TRUSTED_PLATFORM_ASSEMBLIES）按白名单名精确匹配 BCL 引用。
        /// TPA 由 .NET 运行时宿主设置，必然包含 System.Private.CoreLib / System.Runtime / netstandard，
        /// 是预定义类型可解析的可靠来源。白名单外程序集一律跳过（杜绝全量扫描）。
        /// </summary>
        private static int AddBclWhitelist(Dictionary<string, MetadataReference> refs)
        {
            var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (string.IsNullOrEmpty(tpa))
            {
                PluginLogger.Warning(
                    "[MetadataRef] TRUSTED_PLATFORM_ASSEMBLIES 为空，BCL 引用可能缺失（将导致 CS0518）",
                    nameof(MetadataReferenceProvider));
                return 0;
            }

            var whitelist = new HashSet<string>(CoreBclAssemblyNames, StringComparer.OrdinalIgnoreCase);
            int added = 0;
            foreach (var raw in tpa.Split(Path.PathSeparator))
            {
                var path = raw.Trim();
                if (path.Length == 0) continue;
                if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(path)) continue;

                var name = Path.GetFileNameWithoutExtension(path);
                if (!whitelist.Contains(name)) continue;

                if (TryAddRef(refs, path)) added++;
            }

            // mscorlib 不在 TPA（.NET Core 运行时特殊 facade，type-forward 到 System.Private.CoreLib）。
            // halcondotnetxl 等 .NET Framework 时代宿主库引用 mscorlib 2.0.0.0，Roslyn 解析其基类/接口
            // 签名时需 mscorlib 引用做统一（缺失报 CS0012 "Object 未引用"）。从运行时目录显式引用 facade。
            // TryAddRef 内部已做路径去重。
            var mscorlibPath = Path.Combine(
                System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll");
            if (File.Exists(mscorlibPath) && TryAddRef(refs, mscorlibPath)) added++;

            return added;
        }

        /// <summary>
        /// 宿主 API 锚定 — 仅注入 Plugin.SDK 的 Script 基类程序集（系统必需，脚本继承用）。
        /// halcon（HOperatorSet）/ OpenCvSharp（Mat）不再无条件注入编译基线，
        /// 改为按需：用户通过"程序集引用"勾选后，由 <see cref="ResolveAssemblyPath"/> 解析。
        /// 纯类型锚定：typeof(Script).Assembly 反查程序集 Location，编译期确定、不依赖运行时 ALC 加载状态，
        /// 保证脚本继承的 Script 基类类型身份与运行时一致。
        /// </summary>
        private static int AddHostApiAnchored(Dictionary<string, MetadataReference> refs)
        {
            var location = typeof(SunEyeVision.Plugin.SDK.Scripting.Script).Assembly.Location;
            if (!string.IsNullOrEmpty(location) && TryAddRef(refs, location)) return 1;
            return 0;
        }

        /// <summary>
        /// 系统必需引用清单 — 脚本编译的基础依赖（C# 预定义类型 + Script 基类），不可移除。
        /// 仅包含真实存在于 TPA（可信平台程序集）的 BCL + Script 基类所在 SDK 程序集，
        /// 保证默认引用列表每个程序集都能解析到路径（rule-015：找不到直接报错，而非静默跳过）。
        /// 供新节点默认引用列表、UI 系统必需项锁定、编译引用组装使用。
        /// </summary>
        public static IReadOnlyList<string> RequiredAssemblyNames
        {
            get
            {
                var names = new List<string>();

                // BCL 白名单中真实存在于 TPA 的项（过滤 TPA 精简环境下不存在的 BCL）
                var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
                var tpaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(tpa))
                {
                    foreach (var raw in tpa.Split(Path.PathSeparator))
                    {
                        var path = raw.Trim();
                        if (path.Length == 0 || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                        tpaNames.Add(Path.GetFileNameWithoutExtension(path));
                    }
                }
                foreach (var bcl in CoreBclAssemblyNames)
                {
                    if (tpaNames.Contains(bcl)) names.Add(bcl);
                }
                // mscorlib 不在 TPA（.NET Core facade），单独从运行时目录显式引用
                var mscorlibPath = Path.Combine(
                    System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll");
                if (File.Exists(mscorlibPath) && !names.Contains("mscorlib"))
                    names.Add("mscorlib");

                // Script 基类所在 SDK 程序集
                var sdkName = typeof(SunEyeVision.Plugin.SDK.Scripting.Script).Assembly.GetName().Name;
                if (!string.IsNullOrEmpty(sdkName) && !names.Contains(sdkName))
                    names.Add(sdkName);
                return names;
            }
        }

        /// <summary>
        /// 可选宿主程序集候选（halcon / OpenCvSharp）— 供"程序集引用"对话框候选库。
        /// 类型锚定拿路径（typeof(HOperatorSet)/typeof(Mat).Assembly.Location），不依赖运行时 ALC 加载状态。
        /// </summary>
        public static IReadOnlyDictionary<string, string> GetAvailableApis()
        {
            var apis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var anchor in new[]
            {
                (typeof(HOperatorSet), "halcondotnetxl"),
                (typeof(Mat), "OpenCvSharp"),
                (typeof(SunEyeVision.HalAdapter.HalImage), "SunEyeVision.HalAdapter"),
                (typeof(SunEyeVision.CvAdapter.CvImageFactory), "SunEyeVision.CvAdapter"),
            })
            {
                var location = anchor.Item1.Assembly.Location;
                if (!string.IsNullOrEmpty(location))
                    apis[anchor.Item2] = location;
            }
            return apis;
        }

        /// <summary>
        /// 解析程序集名称 → 元数据引用（唯一真相源：每节点 AssemblyReferences 里的程序集名）。
        /// 按名解析来源：
        ///   1. BCL 白名单 → TPA 精确匹配路径
        ///   2. 宿主 API（Script 基类 / halcon / OpenCvSharp）→ 类型锚定路径
        ///   3. 用户 DLL → ScriptRefs 目录（<paramref name="userRefDir"/>）
        /// 找不到 → 抛异常（rule-015 禁止静默回退）。
        /// </summary>
        public static MetadataReference ResolveAssemblyPath(string name, string? userRefDir = null)
        {
            // 1. BCL 白名单：从 TPA 精确匹配
            var bclPath = ResolveBclPath(name);
            if (bclPath != null)
            {
                return MetadataReference.CreateFromFile(bclPath);
            }

            // 2. 宿主 API：类型锚定（Script 基类 / halcon / OpenCvSharp）
            var hostPath = ResolveHostApiPath(name);
            if (hostPath != null)
            {
                return MetadataReference.CreateFromFile(hostPath);
            }

            // 3. 用户 DLL：ScriptRefs 目录
            if (!string.IsNullOrEmpty(userRefDir))
            {
                var userPath = Path.Combine(userRefDir, name);
                if (File.Exists(userPath))
                    return MetadataReference.CreateFromFile(userPath);
            }

            throw new InvalidOperationException(
                $"脚本引用的程序集 '{name}' 无法解析到路径。请通过\"程序集引用\"添加，或确认 ScriptRefs 目录存在该 DLL。");
        }

        /// <summary>
        /// 从 TPA 白名单精确匹配 BCL 程序集路径；不在白名单返回 null。
        /// </summary>
        private static string? ResolveBclPath(string name)
        {
            var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (string.IsNullOrEmpty(tpa)) return null;
            var whitelist = new HashSet<string>(CoreBclAssemblyNames, StringComparer.OrdinalIgnoreCase);
            if (!whitelist.Contains(name)) return null;
            foreach (var raw in tpa.Split(Path.PathSeparator))
            {
                var path = raw.Trim();
                if (path.Length == 0 || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                if (Path.GetFileNameWithoutExtension(path).Equals(name, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            // mscorlib 不在 TPA，从运行时目录显式找 facade
            if (name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase))
            {
                var mscorlibPath = Path.Combine(
                    System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "mscorlib.dll");
                return File.Exists(mscorlibPath) ? mscorlibPath : null;
            }
            return null;
        }

        /// <summary>
        /// 从宿主 API 类型锚定解析程序集路径（Script 基类 / halcon / OpenCvSharp）；未知返回 null。
        /// </summary>
        private static string? ResolveHostApiPath(string name)
        {
            var anchors = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { Path.GetFileNameWithoutExtension(typeof(SunEyeVision.Plugin.SDK.Scripting.Script).Assembly.Location), typeof(SunEyeVision.Plugin.SDK.Scripting.Script) },
                { "halcondotnetxl", typeof(HOperatorSet) },
                { "OpenCvSharp", typeof(Mat) },
                { "SunEyeVision.HalAdapter", typeof(SunEyeVision.HalAdapter.HalImage) },
                { "SunEyeVision.CvAdapter", typeof(SunEyeVision.CvAdapter.CvImageFactory) },
            };
            if (anchors.TryGetValue(name, out var type))
            {
                var location = type.Assembly.Location;
                return string.IsNullOrEmpty(location) ? null : location;
            }
            return null;
        }

        /// <summary>
        /// 加入引用字典（按规范化路径去重）。白名单/锚定已知为托管程序集，
        /// 直接 CreateFromFile 惰性创建（只记路径不读内容，编译时才读元数据），
        /// 不做逐文件 PE 头校验（AssemblyName.GetAssemblyName），杜绝 287 次文件 IO。
        /// </summary>
        private static bool TryAddRef(Dictionary<string, MetadataReference> refs, string path)
        {
            string key;
            try { key = Path.GetFullPath(path); }
            catch { key = path; }
            if (refs.ContainsKey(key)) return false;

            try
            {
                refs[key] = MetadataReference.CreateFromFile(path);
                return true;
            }
            catch (Exception ex)
            {
                PluginLogger.Warning(
                    $"[MetadataRef] 加载引用失败: {path}, 错误: {ex.Message}",
                    nameof(MetadataReferenceProvider));
                return false;
            }
        }
    }
}
