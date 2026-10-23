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
        internal ScriptCompiler.CompileResult? PreparedCompilation { get; set; }
        internal System.Threading.CancellationToken CancellationToken { get; set; }
        internal Func<bool>? ExecutionAdmission { get; set; }
        internal SunEyeVision.Plugin.SDK.Core.ToolExecutionIdentity? ExecutionIdentity { get; set; }
        internal string ExecutionDomain { get; set; } = Guid.NewGuid().ToString("N");
        internal IReadOnlyDictionary<string, Type>? DeclaredOutputs { get; set; }
        private ScriptOutputSnapshot? _ownedOutputs;
        internal ScriptOutputSnapshot? TakeOutputs()
        {
            var outputs = _ownedOutputs;
            _ownedOutputs = null;
            return outputs;
        }
        private readonly Dictionary<string, object?> _outputValues = new();
        private readonly List<string> _logMessages = new();
        private long _compileTimeMs;
        private double _executionTimeMs;
        private readonly object _lock = new();
        private int _runCounter;
        private bool _disposed;
        private bool _callbacksComplete;

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
            var compileResult = PreparedCompilation ?? _compiler.Compile(
                [generatedCode, userCode], assemblyName, assemblyRefDir, assemblyNames);
            if (PreparedCompilation != null && (compileResult.ReferenceSnapshot == null ||
                ScriptCompiler.ComputeCompilationKey(new[] { generatedCode, userCode }, compileResult.ReferenceSnapshot) != compileResult.CacheKey))
                return new List<string> { "获准编译产物与当前源码/生成合同不匹配。" };
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
            if (ExecutionAdmission?.Invoke() == false) throw new InvalidOperationException("运行身份已失效，拒绝加载脚本。");
            //    沙箱按编译缓存键分组：相同代码+引用复用，代码变化自动建新沙箱（旧沙箱由池 LRU 淘汰）。
            ScriptSandbox sandbox;
            ScriptSandboxPool.Lease? lease = null;
            Exception? loadError = null;
            var loadTimer = Stopwatch.StartNew();

            try
            {
                lease = ScriptSandboxPool.Acquire(compileResult.CacheKey, ExecutionDomain, assemblyRefDir, compileResult.ReferenceSnapshot, CancellationToken);
                sandbox = lease.Sandbox;
                sandbox.LoadAssembly(compileResult.AssemblyBytes!, compileResult.SymbolBytes);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == CancellationToken && CancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                loadError = ex;
                sandbox = null!;
            }
            LoadTimeMs = loadTimer.Elapsed.TotalMilliseconds;
            using var executionLease = lease;

            if (loadError != null)
            {
                errors.Add($"加载脚本程序集失败: {loadError.Message}");
                return errors;
            }

            // 验证并转换全部输入后才允许构造用户实例，非法输入不触发用户副作用。
            CancellationToken.ThrowIfCancellationRequested();
            var convertedInputs = new List<(FieldInfo Field, object? Value)>();
            var entryType = sandbox.LoadedAssembly!.GetType(compileResult.EntryClassName);
            foreach (var pair in inputValues ?? new Dictionary<string, object?>())
            {
                var field = entryType?.GetField(pair.Key, BindingFlags.Public | BindingFlags.Instance);
                if (field == null) errors.Add($"脚本中未找到输入字段 \"{pair.Key}\"");
                else
                {
                    try { convertedInputs.Add((field, ConvertType(pair.Value, field.FieldType))); }
                    catch (Exception ex) { errors.Add($"设置输入字段 \"{pair.Key}\" 失败: {ex.Message}"); }
                }
            }
            if (errors.Count != 0) return errors;
            if (ExecutionAdmission?.Invoke() == false) throw new InvalidOperationException("运行身份已失效，拒绝构造脚本实例。");

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
            var scriptContext = new ScriptContext
            {
                CancellationToken = CancellationToken,
                RunId = ExecutionIdentity?.RunId ?? Guid.Empty,
                RootRunId = ExecutionIdentity?.RootRunId ?? Guid.Empty,
                NodeId = ExecutionIdentity?.NodeId,
                ContractIdentity = ExecutionIdentity?.ContractIdentity,
                IsExecutionCurrent = ExecutionAdmission,
                GetGlobalValue = getGlobalValue,
                SetGlobalValue = setGlobalValue,
                LogInfoAction = msg =>
                {
                    PublishLog($"[信息] {msg}");
                },
                LogWarningAction = msg =>
                {
                    PublishLog($"[警告] {msg}");
                },
                LogErrorAction = msg =>
                {
                    PublishLog($"[错误] {msg}");
                }
            };
            scriptInstance.Context = scriptContext;

            // 5. 反射设置输入字段值（在 Init/Process 之前）
            try
            {
                foreach (var input in convertedInputs) input.Field.SetValue(scriptInstance, input.Value);
            }
            catch (Exception ex) { errors.Add($"输入注入失败: {ex.Message}"); }

            // 6. 执行生命周期（Init → Process）
            var runSw = Stopwatch.StartNew();
            OperationCanceledException? cancellation = null;
            try
            {
                if (errors.Count != 0) return errors;
                scriptContext.ThrowIfCancellationRequested();
                var stage = Stopwatch.StartNew();
                try { scriptInstance.Init(); }
                finally { InitTimeMs = stage.Elapsed.TotalMilliseconds; }
                scriptContext.ThrowIfCancellationRequested();
                stage.Restart();
                try { scriptInstance.Process(); }
                finally { ProcessTimeMs = stage.Elapsed.TotalMilliseconds; }
                scriptContext.DrainRegisteredTasks();
                scriptContext.ThrowIfCancellationRequested();
                // 仅声明输出在Release前取得平台自有快照；用户辅助字段不外传。
                _ownedOutputs = new ScriptOutputSnapshot();
                foreach (var output in DeclaredOutputs ?? new Dictionary<string, Type>())
                {
                    var field = scriptInstance.GetType().GetField(output.Key, BindingFlags.Public | BindingFlags.Instance)
                        ?? throw new InvalidOperationException($"未找到声明输出 {output.Key}。");
                    _ownedOutputs.Values[output.Key] = _ownedOutputs.Copy(field.GetValue(scriptInstance), output.Value);
                }
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == CancellationToken && CancellationToken.IsCancellationRequested) { cancellation = ex; }
            catch (Exception ex)
            {
                // 提取用户脚本栈帧（含行号），精确匹配用户类，便于快速定位；无脚本帧则回退完整堆栈文本。
                // 只写 errors（→ CompileErrors → UI 红色展示一次）；不写 _logMessages
                // （用户脚本 LogError 已通过 ScriptContext 回调进日志，避免同一异常重复报告）。
                var detail = ExtractUserScriptFrame(ex, compileResult.EntryClassName!);
                errors.Add($"脚本执行异常: {ex.GetType().Name}: {ex.Message}\n{detail}");
            }
            finally
            {
                var releaseTimer = Stopwatch.StartNew();
                try { scriptContext.DrainRegisteredTasks(); }
                catch (Exception ex) { errors.Add($"登记任务收尾失败: {ex.Message}"); }
                try { scriptInstance.Release(); }
                catch (Exception ex) { errors.Add($"脚本清理异常: {ex.GetType().Name}: {ex.Message}\n{ExtractUserScriptFrame(ex, compileResult.EntryClassName!)}"); }
                finally
                {
                    try { scriptContext.ReleaseRegisteredResources(); }
                    catch (Exception ex) { errors.Add($"登记资源清理失败: {ex.Message}"); }
                    ReleaseTimeMs = releaseTimer.Elapsed.TotalMilliseconds;
                    runSw.Stop();
                    _executionTimeMs = runSw.Elapsed.TotalMilliseconds;
                    lock (_lock) _callbacksComplete = true;
                    scriptContext.CompletePublications();
                    scriptInstance.Context = null!;
                }
            }
            if (cancellation != null)
                throw new OperationCanceledException(errors.Count == 0 ? cancellation.Message :
                    cancellation.Message + "; " + string.Join("; ", errors), cancellation, CancellationToken);
            runSw.Stop();
            _executionTimeMs = runSw.Elapsed.TotalMilliseconds;
            PluginLogger.Info($"[ScriptTiming] key={compileResult.CacheKey}, cacheHit={CompilationCacheHit}, " +
                $"emitMs={EmitTimeMs:F2}, loadMs={LoadTimeMs:F2}, constructMs={ConstructionTimeMs:F2}, initMs={InitTimeMs:F2}, processMs={ProcessTimeMs:F2}, releaseMs={ReleaseTimeMs:F2}, lifecycleMs={_executionTimeMs:F2}", nameof(ScriptLifecycleManager));

            // 7. 反射读取输出字段值
            if (errors.Count != 0)
            {
                try { _ownedOutputs?.Dispose(); } catch (Exception ex) { errors.Add(ex.Message); }
                _ownedOutputs = null;
                return errors;
            }
            foreach (var output in _ownedOutputs!.Values) _outputValues[output.Key] = output.Value;

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
                using var warmupLease = ScriptSandboxPool.Acquire(cacheKey, "warmup:" + cacheKey, assemblyRefDir, compileResult.ReferenceSnapshot, default);
                var sandbox = warmupLease.Sandbox;

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
            if (value == null)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                    throw new InvalidOperationException($"空值不能赋给 {targetType.Name}。");
                return null;
            }
            if (value.GetType() == targetType || targetType.IsInstanceOfType(value))
                return value;

            // 数字类型互转
            if (IsNumericType(value.GetType()) && IsNumericType(targetType))
                return Convert.ChangeType(value, targetType);

            // 字符串转换
            if (targetType == typeof(string))
                return value.ToString();

            throw new InvalidOperationException($"无法把 {value.GetType().Name} 转换为 {targetType.Name}。");
        }

        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float)
                || type == typeof(double) || type == typeof(decimal)
                || type == typeof(short) || type == typeof(byte)
                || type == typeof(uint) || type == typeof(ulong)
                || type == typeof(ushort) || type == typeof(sbyte);
        }

        private void PublishLog(string message)
        {
            void Add() { lock (_lock) if (!_disposed && !_callbacksComplete) _logMessages.Add(message); }
            if (ExecutionIdentity == null) Add();
            else ExecutionIdentity.TryInvokeCallback(Add);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _ownedOutputs?.Dispose();
            _ownedOutputs = null;
            _outputValues.Clear();
        }
    }
}
