using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommonControl;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Tool.Script.ScriptEngine;
using SunEyeVision.Tool.Script.Views;
using ToolExecutionContext = SunEyeVision.Plugin.SDK.Execution.Parameters.ExecutionContext;

namespace SunEyeVision.Tool.Script
{
[Tool(
        id: "Script",
        displayName: "Script",
        Description = "Execute user-defined C# script with DLL references and Init/Process lifecycle",
        Icon = "code", Order = 10,
        Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Script,
        Version = "2.0.0",
        HasDebugWindow = true
    )]
    public class ScriptTool : IToolPlugin<ScriptParameters, ScriptResults>, IAsyncToolPlugin, IToolEditorFactory, IPrecompilableTool
    {
        // ★ 应用启动阶段后台预热补全引擎（MEF + Workspace），对齐新版 RoslynPad 启动初始化。
        //   静态构造在 LoadPlugins 实例化工具时触发，fire-and-forget 不阻塞启动；
        //   首次打开脚本工具时补全立即可用（MEF 已预热缓存命中）。
        static ScriptTool()
        {
            // 编辑器内核统一预热补全引擎（MEF + Workspace），对齐新版 RoslynPad 启动初始化。
            ScriptEditorCore.WarmupForStartup();

            // 预热 Roslyn Emit 引擎（代码无关）：启动时后台编译一次最小模板，
            // 让 Emit 初始化（~947ms）提前完成，用户首次执行脚本 Emit 降到 ~29ms。
            // fire-and-forget 不阻塞启动；失败仅告警（ScriptCompiler.Warmup 内部处理）。
            ScriptCompiler.Warmup();
        }

        public ScriptResults Run(ScriptParameters parameters)
        {
            var frozen = (ScriptParameters)ToolExecutionPreparation.CopyParameters(parameters);
            frozen.PreparedCompilation = parameters.PreparedCompilation;
            frozen.ExecutionAdmission = parameters.ExecutionAdmission;
            var globals = parameters.Context?.GlobalVariables;
            using var globalResources = new ScriptOutputSnapshot();
            var globalSnapshot = SnapshotGlobals(globals, globalResources);
            using var inputs = parameters.NodeResults == null ? null : new ToolResultSnapshot(new Dictionary<string, ToolResults>(parameters.NodeResults));
            var nodes = inputs?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            nodes = SnapshotGlobalBindings(nodes, globalSnapshot);
            if (!frozen.TryPublishIoContract(out _)) return ReleaseGlobalInputs(globalResources, ExecuteCore(frozen, globalSnapshot, nodes, parameters.Context));
            var token = parameters.Context?.CancellationToken ?? default;
            var owner = parameters.RuntimeOwner;
            ScriptSandboxPool.Attach(owner);
            var identity = owner.Begin((parameters.Context?.WorkflowId ?? "direct") + "/" + (parameters.Context?.NodeId ?? "script"),
                Guid.NewGuid(), frozen.IoIdentity, token, frozen.ExecutionAdmission);
            using var active = owner.EnterRun(identity);
            try { return ReleaseGlobalInputs(globalResources, ExecuteCore(frozen, globalSnapshot, nodes, parameters.Context, token, identity, globals)); }
            finally { identity.CompleteCallbacks(); }
        }

        public async ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        {
            var parameters = (ScriptParameters)request.Parameters;
            using var globalResources = new ScriptOutputSnapshot();
            var owner = request.ExecutionOwner ?? new ToolExecutionOwner();
            ScriptSandboxPool.Attach(owner);
            request.ExecutionIdentity = owner.Begin((request.WorkflowId ?? "standalone") + "/" + (request.NodeId ?? "script"),
                request.RunId == Guid.Empty ? Guid.NewGuid() : request.RunId,
                parameters.IoIdentity, request.CancellationToken, parameters.ExecutionAdmission, request.RootRunId);
            if (!parameters.TryPublishIoContract(out var error)) throw new InvalidOperationException(error);
            var globalSnapshot = SnapshotGlobals(request.GlobalVariables, globalResources);
            var nodeSnapshot = SnapshotGlobalBindings(request.NodeResults, globalSnapshot);
            var compiled = parameters.PreparedCompilation ?? await new ScriptCompiler().CompileAsync(
                new[] { parameters.GeneratedCode, parameters.ScriptCode }, "ScriptExecution", parameters.AssemblyReferenceFullPath,
                parameters.AssemblyReferences, request.CancellationToken).ConfigureAwait(false);
            request.CancellationToken.ThrowIfCancellationRequested();
            request.ExecutionIdentity = request.ExecutionIdentity.WithContractIdentity(parameters.IoIdentity + "/" + compiled.CacheKey);
            // 用户代码仍保持逐次实例语义；运行中取消/异常清理的完整合同留S3。
            var result = await Task.Run<ToolResults>(() =>
            {
                if (parameters.ExecutionAdmission?.Invoke() == false) throw new OperationCanceledException("执行请求已过时。");
                using var activeRun = owner.EnterRun(request.ExecutionIdentity);
                try
                {
                    return ExecuteCore(parameters, globalSnapshot, nodeSnapshot, request.Context, request.CancellationToken,
                        request.ExecutionIdentity, request.GlobalVariables, compiled);
                }
                finally { request.ExecutionIdentity.CompleteCallbacks(); }
            }, request.CancellationToken).ConfigureAwait(false);
            return ReleaseGlobalInputs(globalResources, result);
        }

        private static T ReleaseGlobalInputs<T>(ScriptOutputSnapshot inputs, T result) where T : ToolResults
        {
            try
            {
                if (result is ScriptResults script) script.OwnInputs(inputs.TransferResources());
                else inputs.Dispose();
                return result;
            }
            catch
            {
                (result as IDisposable)?.Dispose();
                throw;
            }
        }

        private static Dictionary<string, ToolResults>? SnapshotGlobalBindings(IDictionary<string, ToolResults>? nodes, Dictionary<string, object?>? globals)
        {
            if (globals == null) return nodes == null ? null : new Dictionary<string, ToolResults>(nodes);
            var snapshot = nodes == null ? new Dictionary<string, ToolResults>() : new Dictionary<string, ToolResults>(nodes);
            var result = new GenericToolResults { Status = ExecutionStatus.Success };
            foreach (var pair in globals) result.AddResultItem(pair.Key, pair.Value);
            snapshot["__GlobalVars__"] = result;
            return snapshot;
        }

        private static Dictionary<string, object?>? SnapshotGlobals(IDictionary<string, object?>? globals, ScriptOutputSnapshot resources)
        {
            if (globals == null) return null;
            var snapshot = new Dictionary<string, object?>(globals);
            foreach (var key in snapshot.Keys.ToArray())
            {
                var value = snapshot[key];
                if (value is IDisposable || value is Array)
                    snapshot[key] = resources.Copy(value, value.GetType());
            }
            return snapshot;
        }

        private static ScriptResults ExecuteCore(
            ScriptParameters parameters,
            IDictionary<string, object?>? globalVariables,
            IDictionary<string, ToolResults>? nodeResults,
            ToolExecutionContext? context,
            System.Threading.CancellationToken cancellationToken = default,
            ToolExecutionIdentity? identity = null,
            IDictionary<string, object?>? writeGlobals = null,
            ScriptCompiler.CompileResult? preparedCompilation = null)
        {
            var result = new ScriptResults
            {
                ToolName = "Script",
                ToolId = "Script",
                Timestamp = DateTime.Now,
                ScriptCode = parameters.ScriptCode
            };
            result.ExecutionIdentity = identity;

            try
            {
                if (!parameters.TryPublishIoContract(out var ioError))
                    throw new InvalidOperationException(ioError);
                if (string.IsNullOrWhiteSpace(parameters.ScriptCode))
                {
                    if (parameters.IsDebugMode)
                        result.LogMessages.Add("Script code is empty");   // 调试模式：写调试窗口
                    else
                        LogWarning(context, "Script code is empty");     // 工作流执行：写主界面
                    result.SetError("Script code is empty");
                    return result;
                }

                // 调试模式下不写主界面（摘要进 result.LogMessages 由调试窗口展示），工作流执行保持写主界面。
                if (!parameters.IsDebugMode)
                    LogInfo(context, $"Executing script (lines: {parameters.ScriptCode.Split('\n').Length})");

                using var lifecycleManager = new ScriptLifecycleManager();
                lifecycleManager.CancellationToken = cancellationToken;
                lifecycleManager.ExecutionIdentity = identity;
                lifecycleManager.ExecutionAdmission = identity == null ? parameters.ExecutionAdmission : () => identity.IsCurrent;
                if (identity != null) lifecycleManager.ExecutionDomain = identity.Domain;
                lifecycleManager.DeclaredOutputs = parameters.Outputs.ToDictionary(output => output.Name, output => ScriptIoTypeDescriptor.From(output.TypeName).ClrType, StringComparer.Ordinal);

                // Global variable delegates - bridge from ExecutionContext.GlobalVariables
                var globalVars = globalVariables;
                Func<string, object?>? getGlobal = name =>
                    globalVars?.TryGetValue(name, out var v) == true ? v : null;
                Action<string, object?>? setGlobal = (name, value) =>
                {
                    void Write() { if ((writeGlobals ?? globalVars) is { } target) target[name] = value; }
                    if (identity == null) Write();
                    else if (!identity.TryInvokeCallback(Write)) throw new InvalidOperationException("运行身份失效，拒绝全局变量写入。");
                };

                // ★ 解析输入绑定（从 ExecutionContext.GlobalVariables 读取）
                // 工具调试：ExecutionContext.GlobalVariables 由 ToolExecutor 注入（反射 ServiceInitializer）
                // 工作流执行：ExecutionContext.GlobalVariables + NodeResults 由 AlgorithmNode.Execute 注入
                // NodeResults 支持节点属性绑定（NodeId.PropertyName 格式）
                var inputValues = ScriptBindingResolver.Resolve(
                    parameters.Inputs,
                    globalVariables,
                    nodeResults);

                // ★ 捕获解析后的输入值（供调试监视面板显示）
                parameters.ResolvedInputValues = inputValues;

                if (parameters.Inputs.Count > 0 && !parameters.IsDebugMode)
                {
                    LogInfo(context, $"  解析了 {inputValues.Count} 个输入绑定: " +
                        $"全局变量可用={globalVariables?.Count ?? 0}, " +
                        $"上游节点结果={nodeResults?.Count ?? 0}");
                }

                lifecycleManager.PreparedCompilation = preparedCompilation ?? parameters.PreparedCompilation;
                if (parameters.ExecutionAdmission?.Invoke() == false) throw new OperationCanceledException("输入准备期间请求已过时。");
                var errors = lifecycleManager.Execute(
                    parameters.GeneratedCode,
                    parameters.ScriptCode,
                    parameters.AssemblyReferenceFullPath,
                    getGlobal,
                    setGlobal,
                    inputValues,
                    parameters.AssemblyReferences);

                result.CompileErrors = errors;
                result.CompileTimeMs = lifecycleManager.CompileTimeMs;
                result.ExecutionTimeMs = lifecycleManager.ExecutionTimeMs;
                result.LogMessages = lifecycleManager.LogMessages.ToList();

                // 注入输出变量声明（契约 SSOT），供 ScriptResults.GetOutputVariables 派生运行期变量
                result.OutputConfigs = parameters.Outputs.ToList();

                foreach (var kvp in lifecycleManager.OutputValues)
                {
                    result.OutputValues[kvp.Key] = kvp.Value;
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (identity != null && !identity.IsCurrent) throw new InvalidOperationException("运行身份失效，拒绝发布输出。");
                if (errors.Count == 0) result.OwnOutputs(lifecycleManager.TakeOutputs());

                if (errors.Count == 0)
                {
                    result.SetSuccess(lifecycleManager.ExecutionTimeMs);
                    var summary =
                        $"Script complete | Compile: {lifecycleManager.CompileTimeMs}ms | Exec: {lifecycleManager.ExecutionTimeMs}ms | " +
                        $"Outputs: {result.OutputValues.Count} | Logs: {result.LogMessages.Count}";
                    if (parameters.IsDebugMode)
                        result.LogMessages.Add(summary);   // 调试模式：写调试窗口日志区，不写主界面
                    else
                        LogSuccess(context, summary);     // 工作流执行：写主界面
                }
                else
                {
                    var errorMsg = string.Join("; ", errors.Take(3));
                    if (errors.Count > 3) errorMsg += "...";
                    if (parameters.IsDebugMode)
                        result.LogMessages.Add($"Script failed: {errorMsg}");   // 调试模式：写调试窗口
                    else
                        LogError(context, $"Script failed: {errorMsg}");       // 工作流执行：写主界面
                    result.SetError(errorMsg);
                }
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken && cancellationToken.IsCancellationRequested)
            {
                result.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                try { result.Dispose(); }
                catch (Exception cleanup) { result.CompileErrors.Add("输出回收失败: " + cleanup.Message); }
                // 调试模式：写调试窗口日志区，不写主界面；工作流执行：写主界面（与摘要分流一致）。
                if (parameters.IsDebugMode)
                    result.LogMessages.Add($"Script exception: {ex.Message}");
                else
                    LogError(context, $"Script exception: {ex.Message}", ex);
                result.SetError($"Script exception: {ex.Message}", ex);
            }

            return result;
        }

        private static void LogInfo(ToolExecutionContext? context, string message)
            => context?.Logger?.Info(message, context.GetSourceName());

        private static void LogSuccess(ToolExecutionContext? context, string message)
            => context?.Logger?.Success(message, context.GetSourceName());

        private static void LogWarning(ToolExecutionContext? context, string message)
            => context?.Logger?.Warning(message, context.GetSourceName());

        private static void LogError(ToolExecutionContext? context, string message, Exception? exception = null)
            => context?.Logger?.Error(message, context.GetSourceName(), exception);

        /// <summary>
        /// 预编译并预热执行环境：编译结果写入 <see cref="ScriptCompiler"/> 进程级静态缓存
        /// （命中缓存则跳过编译），编译成功后用同一缓存键预热沙箱常驻池（程序集加载 + 用户代码 JIT）。
        /// 不执行脚本逻辑，无副作用。用于方案加载后/代码变化后预热，保证后续执行
        /// 命中缓存零编译 + 零沙箱/JIT 冷启动（首次与后续执行时间一致）。
        /// 参数非 <see cref="ScriptParameters"/> 或代码为空时直接返回（不阻塞、不抛异常）。
        /// </summary>
        /// <param name="parameters">工具参数（运行时为 <see cref="ScriptParameters"/>）</param>
        /// <returns>编译诊断列表；空 = 编译成功</returns>
        public IReadOnlyList<string> PreCompile(ToolParameters parameters)
        {
            if (parameters is not ScriptParameters scriptParams)
            {
                PluginLogger.Warning("[ScriptTool] PreCompile 参数非 ScriptParameters, 跳过", nameof(ScriptTool));
                return [];
            }

            PluginLogger.Info($"[ScriptTool] PreCompile 开始 | ScriptCode长度={scriptParams.ScriptCode?.Length ?? -1}, GeneratedCode长度={scriptParams.GeneratedCode?.Length ?? -1}, 引用数={scriptParams.AssemblyReferences?.Count ?? -1}", nameof(ScriptTool));

            if (string.IsNullOrWhiteSpace(scriptParams.ScriptCode))
            {
                PluginLogger.Warning("[ScriptTool] PreCompile ScriptCode 为空, 跳过", nameof(ScriptTool));
                return [];
            }

            try
            {
                using var lifecycleManager = new ScriptLifecycleManager();
                var errors = lifecycleManager.CompileOnly(
                    scriptParams.GeneratedCode,
                    scriptParams.ScriptCode,
                    scriptParams.AssemblyReferenceFullPath,
                    scriptParams.AssemblyReferences);

                if (errors.Count > 0)
                {
                    scriptParams.LogWarning($"[ScriptTool] 预编译发现错误: {string.Join("; ", errors.Take(3))}");
                }
                else
                {
                    // 编译成功 → 预热执行环境（沙箱 + 程序集加载 + 用户代码 JIT）：
                    // 用同一缓存键复用刚入缓存的编译产物，首次执行命中常驻沙箱零冷启动。
                    lifecycleManager.WarmupSandbox(
                        scriptParams.GeneratedCode,
                        scriptParams.ScriptCode,
                        scriptParams.AssemblyReferenceFullPath,
                        scriptParams.AssemblyReferences);
                }

                return errors;
            }
            catch (Exception ex)
            {
                scriptParams.LogWarning($"[ScriptTool] 预编译异常: {ex.Message}");
                return [ex.Message];
            }
        }

        public FrameworkElement? CreateDebugControl(IToolPlugin tool)
        {
            return new ScriptDebugControl();
        }
    }
}
