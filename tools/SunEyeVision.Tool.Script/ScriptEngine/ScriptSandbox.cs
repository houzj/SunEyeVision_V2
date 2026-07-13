using System;
using System.IO;
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
    /// 程序集可见性：构造函数预加载 ScriptRefs 目录下的用户第三方 DLL。
    /// 业务程序集（halcondotnetxl/OpenCvSharp/用户自定义）走 ScriptRefs，
    /// .NET BCL 类型走默认 ALC 自动 fallback。
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

        public ScriptSandbox(string contextName, string? assemblyRefDir = null)
        {
            _contextName = contextName;
            _alc = new AssemblyLoadContext(contextName, isCollectible: true);

            // 预加载 ScriptRefs 目录中的所有用户自定义 DLL
            if (!string.IsNullOrEmpty(assemblyRefDir) && Directory.Exists(assemblyRefDir))
            {
                foreach (var dll in Directory.EnumerateFiles(assemblyRefDir, "*.dll"))
                {
                    try
                    {
                        _alc.LoadFromAssemblyPath(dll);
                    }
                    catch (Exception ex)
                    {
                        PluginLogger.Warning(
                            $"[Sandbox] 预加载 ScriptRefs DLL 失败: {dll}, 错误={ex.Message}",
                            nameof(ScriptSandbox));
                    }
                }
            }

            // OnResolving 兜底（rule-008 报错优先）：
            // ScriptRefs 已预加载 halcondotnetxl/OpenCvSharp/SWF 等业务 DLL。
            // 未预加载的类型（如 SunEyeVision.*，由默认 ALC fallback 自动解析）
            // 触发本回调时返回 null，让 ALC 抛 FileNotFoundException，
            // 脚本作者立刻知道哪个类型不可用（不静默）。
            _alc.Resolving += OnResolving;
        }

        /// <summary>
        /// 从字节数组加载脚本程序集到沙箱
        /// </summary>
        public Assembly LoadAssembly(byte[] assemblyBytes, byte[]? symbolBytes = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ScriptSandbox));

            if (symbolBytes != null)
                LoadedAssembly = _alc.LoadFromStream(new MemoryStream(assemblyBytes), new MemoryStream(symbolBytes));
            else
                LoadedAssembly = _alc.LoadFromStream(new MemoryStream(assemblyBytes));

            return LoadedAssembly;
        }

        /// <summary>
        /// 创建脚本实例（必须已调用 LoadAssembly）
        /// </summary>
        public ScriptBase? CreateScriptInstance(string className)
        {
            if (LoadedAssembly == null)
                throw new InvalidOperationException("请先调用 LoadAssembly 加载程序集");

            var type = LoadedAssembly.GetType(className);
            if (type == null)
                return null;

            return Activator.CreateInstance(type) as ScriptBase;
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
        /// ScriptRefs 已预加载业务 DLL，未预加载的类型触发的 resolve 事件
        /// 返回 null，让 ALC 抛 FileNotFoundException（不静默）。
        /// </summary>
        private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            return null;
        }

        public void Dispose()
        {
            Unload();
        }
    }
}
