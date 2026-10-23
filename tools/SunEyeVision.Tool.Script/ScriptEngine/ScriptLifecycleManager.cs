using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Scripting;
using ScriptBase = SunEyeVision.Plugin.SDK.Scripting.Script;

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
        private double _executionTimeMs;
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
        /// 执行耗时（毫秒，保留亚毫秒精度）
        /// </summary>
        public double ExecutionTimeMs => _executionTimeMs;
        public double LoadTimeMs { get; private set; }
        public double ConstructionTimeMs { get; private set; }
        public bool CompilationCacheHit { get; private set; }
        public double EmitTimeMs { get; private set; }
        public double InitTimeMs { get; private set; }
        public double ProcessTimeMs { get; private set; }
        public double ReleaseTimeMs { get; private set; }

        /// <summary>
        /// 执行指定脚本代码（使用 GeneratedCode + ScriptCode 双段 partial class 编译）
        /// </summary>
        /// <param name="generatedCode">系统生成的字段声明 partial class 代码</param>
        /// <param name="userCode">用户编写的完整 partial class 代码</param>
        /// <param name="assemblyRefDir">程序集引用目录路径（用户 DLL ScriptRefs 来源）</param>
        /// <param name="getGlobalValue">获取全局变量委托</param>
        /// <param name="setGlobalValue">设置全局变量委托</param>
        /// <param name="inputValues">输入字段值映射（名称→值，反射设置到脚本实例字段）</param>
        /// <param name="assemblyNames">每节点启用的程序集引用列表（唯一真相源）；不提供则编译回退到宿主引用+ScriptRefs</param>
        /// <returns>编译和执行错误信息列表</returns>
        public List<string> Execute(
            string generatedCode,
            string userCode,
            string? assemblyRefDir,
            Func<string, object?>? getGlobalValue,
            Action<string, object?>? setGlobalValue,
            Dictionary<string, object?>? inputValues = null,
            IReadOnlyList<string>? assemblyNames = null)
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
                LoadTimeMs = ConstructionTimeMs = EmitTimeMs = 0;
                InitTimeMs = ProcessTimeMs = ReleaseTimeMs = 0;
                CompilationCacheHit = false;
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
                [generatedCode, userCode], assemblyName, assemblyRefDir, assemblyNames);
            compileSw.Stop();
            _compileTimeMs = compileSw.ElapsedMilliseconds;
            CompilationCacheHit = compileResult.CacheHit;
            EmitTimeMs = compileResult.EmitMs;

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

            // 2. 在沙箱中加载并执行（从常驻池获取，复用 ALC + 程序集 + JIT 缓存）。
            //    沙箱按编译缓存键分组：相同代码+引用复用，代码变化自动建新沙箱（旧沙箱由池 LRU 淘汰）。
            ScriptSandbox sandbox;
            Exception? loadError = null;
            var loadTimer = Stopwatch.StartNew();

            try
            {
                sandbox = ScriptSandboxPool.GetOrCreate(compileResult.CacheKey, assemblyRefDir, compileResult.ReferenceSnapshot);
                sandbox.LoadAssembly(compileResult.AssemblyBytes!, compileResult.SymbolBytes);
            }
            catch (Exception ex)
            {
                loadError = ex;
                sandbox = null!;
            }
            LoadTimeMs = loadTimer.Elapsed.TotalMilliseconds;

            if (loadError != null)
            {
                errors.Add($"加载脚本程序集失败: {loadError.Message}");
                return errors;
            }

            // 3. 创建实例（类不存在或类型不匹配时 CreateScriptInstance 直接抛异常，错误信息已含诊断上下文）
            ScriptBase scriptInstance;
            var constructTimer = Stopwatch.StartNew();
            try
            {
                scriptInstance = sandbox.CreateScriptInstance(compileResult.EntryClassName);
            }
            catch (Exception ex)
            {
                errors.Add($"创建脚本实例失败: {ex.Message}");
                return errors;
            }
            finally { ConstructionTimeMs = constructTimer.Elapsed.TotalMilliseconds; }

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
                var stage = Stopwatch.StartNew();
                try { scriptInstance.Init(); }
                finally { InitTimeMs = stage.Elapsed.TotalMilliseconds; }
                stage.Restart();
                try { scriptInstance.Process(); }
                finally { ProcessTimeMs = stage.Elapsed.TotalMilliseconds; }
                stage.Restart();
                try { scriptInstance.Release(); }
                finally { ReleaseTimeMs = stage.Elapsed.TotalMilliseconds; }
            }
            catch (Exception ex)
            {
                // 提取用户脚本栈帧（含行号），精确匹配用户类，便于快速定位；无脚本帧则回退完整堆栈文本。
                // 只写 errors（→ CompileErrors → UI 红色展示一次）；不写 _logMessages
                // （用户脚本 LogError 已通过 ScriptContext 回调进日志，避免同一异常重复报告）。
                var detail = ExtractUserScriptFrame(ex, compileResult.EntryClassName!);
                errors.Add($"脚本执行异常: {detail}");
            }
            runSw.Stop();
            _executionTimeMs = runSw.Elapsed.TotalMilliseconds;
            PluginLogger.Info($"[ScriptTiming] key={compileResult.CacheKey}, cacheHit={CompilationCacheHit}, " +
                $"emitMs={EmitTimeMs:F2}, loadMs={LoadTimeMs:F2}, constructMs={ConstructionTimeMs:F2}, initMs={InitTimeMs:F2}, processMs={ProcessTimeMs:F2}, releaseMs={ReleaseTimeMs:F2}, lifecycleMs={_executionTimeMs:F2}", nameof(ScriptLifecycleManager));

            // 7. 反射读取输出字段值
            var outputType = scriptInstance.GetType();
            foreach (var field in outputType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                // 跳过 Script 基类继承的字段（如果有）
                if (field.DeclaringType == typeof(ScriptBase)) continue;

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
        /// 仅编译并预热缓存（命中缓存则跳过，未命中则编译并入缓存），不创建沙箱/实例。
        /// 用于预编译场景：方案加载后/代码变化后预热脚本编译结果，保证后续执行命中缓存零编译。
        /// 编译结果写入 <see cref="ScriptCompiler"/> 的进程级静态缓存，工具调试与工作流执行共用。
        /// </summary>
        /// <param name="generatedCode">系统生成的字段声明 partial class 代码</param>
        /// <param name="userCode">用户编写的完整 partial class 代码</param>
        /// <param name="assemblyRefDir">程序集引用目录路径（用户 DLL ScriptRefs 来源）</param>
        /// <param name="assemblyNames">每节点启用的程序集引用列表（唯一真相源）</param>
        /// <returns>编译诊断列表（空 = 编译成功）；调用方仅用于诊断，不阻塞</returns>
        public List<string> CompileOnly(
            string generatedCode,
            string userCode,
            string? assemblyRefDir,
            IReadOnlyList<string>? assemblyNames = null)
        {
            if (string.IsNullOrWhiteSpace(generatedCode) && string.IsNullOrWhiteSpace(userCode))
            {
                return ["脚本代码为空"];
            }

            var assemblyName = $"ScriptPreCompile_{Guid.NewGuid():N}";
            var result = _compiler.Compile(
                [generatedCode, userCode], assemblyName, assemblyRefDir, assemblyNames);

            return result.Success ? [] : result.Diagnostics;
        }

        /// <summary>
        /// 预热执行环境（沙箱 + 程序集加载 + 用户代码 JIT），供预编译成功后调用。
        /// 原理：预编译已把实际编译产物写入进程级编译缓存，此处按同一缓存键
        /// 复用该产物完成沙箱常驻池就位、程序集加载、实例化与 PrepareMethod 预编译
        /// 方法体（只 JIT 不执行，无副作用）。这样首次执行不再承担沙箱创建/
        /// 程序集加载/用户代码 JIT 的冷启动开销，实现"首次与后续执行时间一致"。
        /// 防御：编译结果缺失/代码为空/预热异常均仅告警返回，不抛异常不阻塞。
        /// </summary>
        /// <param name="generatedCode">系统生成的字段声明 partial class 代码</param>
        /// <param name="userCode">用户编写的完整 partial class 代码</param>
        /// <param name="assemblyRefDir">程序集引用目录路径（用户 DLL ScriptRefs 来源）</param>
        /// <param name="assemblyNames">每节点启用的程序集引用列表（唯一真相源）</param>
        public void WarmupSandbox(
            string generatedCode,
            string userCode,
            string? assemblyRefDir,
            IReadOnlyList<string>? assemblyNames = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(generatedCode) && string.IsNullOrWhiteSpace(userCode))
                {
                    return;
                }

                var combinedCode = string.Join("\n", new[] { generatedCode, userCode });
                var snapshot = ScriptReferenceSnapshot.Capture(assemblyNames, assemblyRefDir);
                var cacheKey = ScriptCompiler.ComputeCompilationKey(new[] { generatedCode, userCode }, snapshot);
                var compileResult = ScriptCompiler.GetCachedResult(cacheKey);
                if (compileResult?.Success != true || compileResult.AssemblyBytes == null
                    || compileResult.EntryClassName == null)
                {
                    // 编译结果未入缓存（含编译失败场景），无可预热的实际产物，静默跳过
                    return;
                }

                // 1. 常驻沙箱按实际缓存键就位（后续执行 GetOrCreate 命中，跳过创建）
                var sandbox = ScriptSandboxPool.GetOrCreate(cacheKey, assemblyRefDir, compileResult.ReferenceSnapshot);

                // 2. 加载实际程序集（后续执行 LoadAssembly 命中，跳过加载）
                sandbox.LoadAssembly(compileResult.AssemblyBytes, compileResult.SymbolBytes);

                // 3. 实例化触发构造函数 JIT + PrepareMethod 预编译方法体（不执行，无副作用）
                var scriptType = sandbox.LoadedAssembly!.GetType(compileResult.EntryClassName, throwOnError: true)!;
                foreach (var methodName in new[] { "Init", "Process", "Release" })
                {
                    var method = scriptType.GetMethod(
                        methodName, BindingFlags.Public | BindingFlags.Instance);
                    if (method != null)
                    {
                        RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLogger.Warning($"[Sandbox预热] 失败: {ex.Message}", nameof(ScriptLifecycleManager));
            }
        }

        /// <summary>
        /// 从异常堆栈中提取用户脚本栈帧（类名+方法名+行号），便于用户快速定位出错行。
        /// 程序集编译带 PortablePdb（SymbolBytes），运行时 StackTrace 可解析行号。
        /// 仅匹配用户脚本类名（EntryClassName），避免框架帧（如 ScriptLifecycleManager）被误收。
        /// 未命中用户脚本帧时返回完整堆栈文本（Fail-Forward，不丢失定位线索）。
        /// </summary>
        /// <param name="ex">用户脚本执行时抛出的异常</param>
        /// <param name="className">用户脚本类名（EntryClassName），仅匹配该类帧</param>
        /// <returns>用户脚本栈帧文本；无脚本帧时返回完整堆栈文本</returns>
        private static string ExtractUserScriptFrame(Exception ex, string className)
        {
            var trace = ex.StackTrace;
            if (string.IsNullOrWhiteSpace(trace))
                return ex.ToString();

            // StackTrace 形如 "at UserScript.Process() in <anonymous> :line 25"
            // 提取包含用户脚本类名的帧，优先展示含行号的脚本调用点。
            var lines = trace.Split('\n');
            var scriptLines = lines
                .Where(l => l.Contains(className, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return scriptLines.Count > 0
                ? string.Join("\n", scriptLines)
                : trace;
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
