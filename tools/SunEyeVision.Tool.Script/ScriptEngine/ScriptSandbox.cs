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
    /// 程序集可见性：构造函数一次性预加载宿主 ALC 的所有脚本相关程序集（按路径加载），
    /// 以及 ScriptRefs 目录下的用户第三方 DLL。按路径加载避免 .NET 8 facade 按名解析歧义。
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

            // 预加载宿主 ALC 中与脚本相关的所有程序集（按路径，避免 .NET 8 facade 解析歧义）
            var hostPaths = MetadataReferenceProvider.GetHostAssemblyPaths();
            foreach (var path in hostPaths)
            {
                try
                {
                    _alc.LoadFromAssemblyPath(path);
                }
                catch (Exception ex)
                {
                    PluginLogger.Warning(
                        $"[Sandbox] 预加载宿主程序集失败: {path}, 错误={ex.Message}",
                        nameof(ScriptSandbox));
                }
            }

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

            // OnResolving 兜底：仅用于诊断，正常路径下所有程序集已预加载到 _alc
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
        /// 所有宿主程序集已在构造函数预加载，正常路径下不会触发本回调。
        /// 若触发，说明预加载遗漏 → 返回 null 让 ALC 抛 FileNotFoundException，
        /// 让脚本作者立刻知道哪个类型不可用（不静默）。
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
