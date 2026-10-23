using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using ScriptBase = SunEyeVision.Plugin.SDK.Scripting.Script;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 脚本沙箱 — 使用 AssemblyLoadContext 隔离加载脚本程序集。
    /// 每次执行后卸载，防止内存泄漏。
    ///
    /// 程序集可见性（遵循 .NET 官方插件隔离模式）：
    ///   宿主系统 DLL（已在默认 ALC 中加载的 SunEyeVision.* / halcon / OpenCvSharp 等）
    ///   必须由默认 ALC fallback 解析，禁止预加载到自定义 ALC，否则脚本继承的
    ///   Script 基类与沙箱 as ScriptBase 校验时类型身份不一致 → 静默失败。
    ///   仅用户自定义第三方 DLL（来自 userLibDir，通常为 ScriptRefs 中的用户 DLL）
    ///   才预加载到自定义 ALC 实现隔离。
    /// </summary>
    internal sealed class ScriptSandbox : IDisposable
    {
        private readonly AssemblyLoadContext _alc;
        private readonly string _contextName;
        private bool _disposed;

        /// <summary>
        /// 已加载的脚本程序集
        /// </summary>
        public Assembly? LoadedAssembly { get; private set; }

        public ScriptSandbox(string contextName, string? userLibDir = null, ScriptReferenceSnapshot? references = null)
        {
            _contextName = contextName;
            _alc = new AssemblyLoadContext(contextName, isCollectible: true);

            // 只加载本次选中的用户引用字节；目录其他 DLL 不得混入运行环境。
            try
            {
            if (references != null)
                foreach (var image in references.Entries.Where(e => e.IsUserReference))
                {
                    if (IsHostAssembly(image.AssemblyName))
                        throw new InvalidOperationException($"用户引用 '{image.AssemblyName}' 与宿主已加载程序集冲突，请使用不同程序集名或重启应用。");
                    using var stream = new MemoryStream(image.Image.ToArray(), writable: false);
                    _alc.LoadFromStream(stream);
                }
            }
            catch
            {
                _alc.Unload();
                throw;
            }

            // OnResolving 兜底（rule-008 报错优先）：
            // 脚本引用但未预加载的 DLL 触发本回调，记录诊断信息后返回 null，
            // 让 CLR 继续走默认 ALC fallback 或抛 FileNotFoundException（不静默）。
            _alc.Resolving += OnResolving;
        }

        /// <summary>
        /// 判断程序集是否为宿主已加载程序集（已在默认 ALC 中存在）。
        /// 宿主程序集禁止复制到自定义 ALC，必须由默认 ALC fallback 解析以保持类型一致。
        /// </summary>
        private static bool IsHostAssembly(string assemblyName)
        {
            return AssemblyLoadContext.Default.Assemblies
                .Any(a => !a.IsDynamic && a.GetName().Name == assemblyName);
        }

        /// <summary>
        /// 从字节数组加载脚本程序集到沙箱
        /// </summary>
        public Assembly LoadAssembly(byte[] assemblyBytes, byte[]? symbolBytes = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ScriptSandbox));

            if (LoadedAssembly != null) return LoadedAssembly;
            if (symbolBytes != null)
                LoadedAssembly = _alc.LoadFromStream(new MemoryStream(assemblyBytes), new MemoryStream(symbolBytes));
            else
                LoadedAssembly = _alc.LoadFromStream(new MemoryStream(assemblyBytes));

            return LoadedAssembly;
        }

        /// <summary>
        /// 创建脚本实例（必须已调用 LoadAssembly）。
        /// 类不存在或类型不匹配时直接抛异常（不静默返回 null），错误信息包含 ALC 上下文。
        /// </summary>
        public ScriptBase CreateScriptInstance(string className)
        {
            if (LoadedAssembly == null)
                throw new InvalidOperationException("请先调用 LoadAssembly 加载程序集");

            var type = LoadedAssembly.GetType(className)
                ?? throw new InvalidOperationException(
                    $"程序集中找不到类 \"{className}\"。请确认脚本代码中有 public class {className} : Script 的定义。");

            var instance = Activator.CreateInstance(type);

            if (instance is not ScriptBase scriptInstance)
                throw new InvalidOperationException(
                    $"类 \"{className}\" 类型不匹配，无法作为脚本实例。\n" +
                    $"  实际基类: {type.BaseType?.FullName}\n" +
                    $"  基类所在程序集: {type.BaseType?.Assembly.GetName().Name}\n" +
                    $"  宿主 ScriptBase 程序集: {typeof(ScriptBase).Assembly.GetName().Name}\n" +
                    $"原因：宿主 DLL 被错误预加载到了自定义 ALC，导致基类类型出现两份副本。");

            return scriptInstance;
        }

        /// <summary>
        /// 卸载沙箱（释放脚本程序集）
        /// </summary>
        public void Unload()
        {
            if (!_disposed)
            {
                _disposed = true;
                _alc.Resolving -= OnResolving;
                LoadedAssembly = null;
                _alc.Unload();
            }
        }

        /// <summary>
        /// OnResolving 兜底（rule-008 报错优先）：
        /// 记录无法解析的程序集全名与自定义 ALC 已加载列表，便于定位缺失的 DLL，
        /// 然后返回 null 让 CLR 继续走默认 ALC fallback 或抛 FileNotFoundException（不静默）。
        /// </summary>
        private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            PluginLogger.Error(
                $"[Sandbox] 程序集解析失败: \"{assemblyName.FullName}\"\n" +
                $"  上下文: {_contextName}\n" +
                $"  自定义 ALC 已加载: [{string.Join(", ", context.Assemblies.Select(a => a.GetName().Name))}]\n" +
                $"  提示: 宿主系统 DLL (SunEyeVision.*/halcon/OpenCvSharp) 通过默认 ALC fallback 解析；" +
                $"用户自定义 DLL 请放置到 ScriptRefs 目录。",
                nameof(ScriptSandbox));
            return null;
        }

        public void Dispose()
        {
            Unload();
        }
    }
}
