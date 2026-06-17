using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 脚本生命周期管理器 — 协调编译→加载→Init→Process→Release 流程。
    /// 每次 Run 创建独立沙箱，执行后自动卸载。
    /// </summary>
    internal sealed class ScriptLifecycleManager : IDisposable
    {
        private readonly ScriptCompiler _compiler = new();
        private readonly Dictionary<string, object?> _outputValues = new();
        private readonly List<string> _logMessages = new();
        private long _compileTimeMs;
        private long _executionTimeMs;
        private readonly object _lock = new();
        private int _runCounter;
        private bool _disposed;

        /// <summary>
        /// 输出值字典（执行结束后可读取）
        /// </summary>
        public IReadOnlyDictionary<string, object?> OutputValues => _outputValues;

        /// <summary>
        /// 日志信息列表
        /// </summary>
        public IReadOnlyList<string> LogMessages => _logMessages;

        /// <summary>
        /// 编译耗时
        /// </summary>
        public long CompileTimeMs => _compileTimeMs;

        /// <summary>
        /// 执行耗时
        /// </summary>
        public long ExecutionTimeMs => _executionTimeMs;

        /// <summary>
        /// 执行指定脚本代码
        /// </summary>
        /// <param name="code">脚本代码</param>
        /// <param name="assemblyRefDir">程序集引用目录路径（目录下所有 *.dll 均为引用）</param>
        /// <param name="getGlobalValue">获取全局变量委托</param>
        /// <param name="setGlobalValue">设置全局变量委托</param>
        /// <returns>编译和执行错误信息列表</returns>
        public List<string> Execute(
            string code,
            string? assemblyRefDir,
            Func<string, object?>? getGlobalValue,
            Action<string, object?>? setGlobalValue)
        {
            var errors = new List<string>();

            if (_disposed)
            {
                errors.Add("ScriptLifecycleManager 已释放");
                return errors;
            }

            lock (_lock)
            {
                _outputValues.Clear();
                _logMessages.Clear();
                _compileTimeMs = 0;
                _executionTimeMs = 0;
                _runCounter++;
            }

            var assemblyName = $"ScriptAssembly_{_runCounter}_{Guid.NewGuid():N}";

            // 1. 编译
            var compileSw = Stopwatch.StartNew();
            var compileResult = _compiler.Compile(code, assemblyName, assemblyRefDir);
            compileSw.Stop();
            _compileTimeMs = compileSw.ElapsedMilliseconds;

            if (!compileResult.Success)
            {
                errors.AddRange(compileResult.Diagnostics);
                return errors;
            }

            if (compileResult.EntryClassName == null)
            {
                errors.Add("未找到继承 Script 的类。请确保脚本中有一个类继承 Script。");
                return errors;
            }

            // 2. 在沙箱中加载并执行
            using var sandbox = new ScriptSandbox($"ScriptSandbox_{_runCounter}");
            Exception? loadError = null;

            try
            {
                sandbox.LoadAssembly(compileResult.AssemblyBytes!, compileResult.SymbolBytes);
            }
            catch (Exception ex)
            {
                loadError = ex;
            }

            if (loadError != null)
            {
                errors.Add($"加载脚本程序集失败: {loadError.Message}");
                return errors;
            }

            // 3. 创建实例
            var scriptInstance = sandbox.CreateScriptInstance(compileResult.EntryClassName);
            if (scriptInstance == null)
            {
                errors.Add($"创建脚本实例失败: 找不到类 \"{compileResult.EntryClassName}\"");
                return errors;
            }

            // 4. 注入上下文
            scriptInstance.Context = new ScriptContext
            {
                GetGlobalValue = getGlobalValue,
                SetGlobalValue = setGlobalValue,
                SetOutputValue = (name, value) =>
                {
                    lock (_lock)
                    {
                        _outputValues[name] = value;
                    }
                },
                LogInfoAction = msg =>
                {
                    lock (_lock)
                    {
                        _logMessages.Add($"[信息] {msg}");
                    }
                },
                LogWarningAction = msg =>
                {
                    lock (_lock)
                    {
                        _logMessages.Add($"[警告] {msg}");
                    }
                },
                LogErrorAction = msg =>
                {
                    lock (_lock)
                    {
                        _logMessages.Add($"[错误] {msg}");
                    }
                }
            };

            // 5. 执行三段式生命周期
            var runSw = Stopwatch.StartNew();
            try
            {
                scriptInstance.Init();
                scriptInstance.Process();
                scriptInstance.Release();
            }
            catch (Exception ex)
            {
                errors.Add($"脚本执行异常: {ex.Message}");
                lock (_lock)
                {
                    _logMessages.Add($"[错误] 脚本执行异常: {ex.Message}");
                }
            }
            runSw.Stop();
            _executionTimeMs = runSw.ElapsedMilliseconds;

            return errors;
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
