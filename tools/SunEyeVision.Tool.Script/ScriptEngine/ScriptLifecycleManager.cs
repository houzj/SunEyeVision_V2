using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 脚本生命周期管理器 — 协调编译→加载→字段注入→Init→Process→字段读取 流程。
    /// 每次 Run 创建独立沙箱，执行后自动卸载。
    /// 输入/输出通过 partial class 字段反射读写，无需 SetOutput 中间层。
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
        /// 执行指定脚本代码（使用 GeneratedCode + ScriptCode 双段 partial class 编译）
        /// </summary>
        /// <param name="generatedCode">系统生成的字段声明 partial class 代码</param>
        /// <param name="userCode">用户编写的完整 partial class 代码</param>
        /// <param name="assemblyRefDir">程序集引用目录路径（目录下所有 *.dll 均为引用）</param>
        /// <param name="getGlobalValue">获取全局变量委托</param>
        /// <param name="setGlobalValue">设置全局变量委托</param>
        /// <param name="inputValues">输入字段值映射（名称→值，反射设置到脚本实例字段）</param>
        /// <returns>编译和执行错误信息列表</returns>
        public List<string> Execute(
            string generatedCode,
            string userCode,
            string? assemblyRefDir,
            Func<string, object?>? getGlobalValue,
            Action<string, object?>? setGlobalValue,
            Dictionary<string, object?>? inputValues = null)
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

            if (string.IsNullOrWhiteSpace(generatedCode) && string.IsNullOrWhiteSpace(userCode))
            {
                errors.Add("脚本代码为空");
                return errors;
            }

            var assemblyName = $"ScriptAssembly_{_runCounter}_{Guid.NewGuid():N}";

            // 1. 编译（双语法树：系统生成的字段声明 + 用户完整 partial class）
            var compileSw = Stopwatch.StartNew();
            var compileResult = _compiler.Compile(
                [generatedCode, userCode], assemblyName, assemblyRefDir);
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

            // 4. 注入上下文（日志 + 全局变量）
            scriptInstance.Context = new ScriptContext
            {
                GetGlobalValue = getGlobalValue,
                SetGlobalValue = setGlobalValue,
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

            // 5. 反射设置输入字段值（在 Init/Process 之前）
            if (inputValues != null && inputValues.Count > 0)
            {
                var scriptType = scriptInstance.GetType();
                foreach (var kvp in inputValues)
                {
                    var field = scriptType.GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null)
                    {
                        try
                        {
                            var value = ConvertType(kvp.Value, field.FieldType);
                            field.SetValue(scriptInstance, value);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"设置输入字段 \"{kvp.Key}\" 失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        errors.Add($"脚本中未找到输入字段 \"{kvp.Key}\"");
                    }
                }
            }

            // 6. 执行生命周期（Init → Process）
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

            // 7. 反射读取输出字段值
            var outputType = scriptInstance.GetType();
            foreach (var field in outputType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                // 跳过 Script 基类继承的字段（如果有）
                if (field.DeclaringType == typeof(Script)) continue;

                // 只取 partial class 中声明的 public 字段
                var value = field.GetValue(scriptInstance);
                lock (_lock)
                {
                    _outputValues[field.Name] = value;
                }
            }

            return errors;
        }

        /// <summary>
        /// 类型转换：将 object? 值转换到目标字段类型
        /// </summary>
        private static object? ConvertType(object? value, Type targetType)
        {
            if (value == null) return null;
            if (value.GetType() == targetType || targetType.IsInstanceOfType(value))
                return value;

            // 数字类型互转
            if (IsNumericType(value.GetType()) && IsNumericType(targetType))
                return Convert.ChangeType(value, targetType);

            // 字符串转换
            if (targetType == typeof(string))
                return value.ToString();

            return value;
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float)
                || type == typeof(double) || type == typeof(decimal)
                || type == typeof(short) || type == typeof(byte)
                || type == typeof(uint) || type == typeof(ulong)
                || type == typeof(ushort) || type == typeof(sbyte);
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
