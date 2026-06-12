using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 脚本沙箱 — 使用 AssemblyLoadContext 隔离加载脚本程序集，
    /// 每次执行后卸载，防止内存泄漏。
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

        public ScriptSandbox(string contextName)
        {
            _contextName = contextName;
            _alc = new AssemblyLoadContext(contextName, isCollectible: true);

            // 配置程序集解析：优先从默认上下文加载
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
        public Script? CreateScriptInstance(string className)
        {
            if (LoadedAssembly == null)
                throw new InvalidOperationException("请先调用 LoadAssembly 加载程序集");

            var type = LoadedAssembly.GetType(className);
            if (type == null)
                return null;

            return Activator.CreateInstance(type) as Script;
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

        private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            // 尝试从默认 ALC 解析（框架程序集）
            try
            {
                return Assembly.Load(assemblyName);
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            Unload();
        }
    }
}
