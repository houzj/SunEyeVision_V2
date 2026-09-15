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
        Icon = "S",
        Category = "Tools",
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
            => ExecuteCore(
                parameters,
                parameters.Context?.GlobalVariables,
                parameters.NodeResults,
                parameters.Context);

        public ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request)
        {
            var result = ExecuteCore(
                (ScriptParameters)request.Parameters,
                request.GlobalVariables,
                request.NodeResults,
                request.Context);
            return ValueTask.FromResult<ToolResults>(result);
        }

        private static ScriptResults ExecuteCore(
            ScriptParameters parameters,
            IDictionary<string, object?>? globalVariables,
            IDictionary<string, ToolResults>? nodeResults,
            ToolExecutionContext? context)
        {
            var result = new ScriptResults
            {
                ToolName = "Script",
                ToolId = "Script",
                Timestamp = DateTime.Now,
                ScriptCode = parameters.ScriptCode
            };

            try
            {
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

                // Global variable delegates - bridge from ExecutionContext.GlobalVariables
                var globalVars = globalVariables;
                Func<string, object?>? getGlobal = name =>
                    globalVars?.TryGetValue(name, out var v) == true ? v : null;
                Action<string, object?>? setGlobal = (name, value) =>
                {
                    if (globalVars != null)
                        globalVars[name] = value;
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
            catch (Exception ex)
            {
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
