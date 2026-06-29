using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Tool.Script.ScriptEngine;
using SunEyeVision.Tool.Script.Views;

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
    public class ScriptTool : IToolPlugin<ScriptParameters, ScriptResults>
    {
        public ScriptResults Run(ScriptParameters parameters)
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
                    parameters.LogWarning("Script code is empty");
                    result.SetError("Script code is empty");
                    return result;
                }

                parameters.LogInfo($"Executing script (lines: {parameters.ScriptCode.Split('\n').Length})");

                using var lifecycleManager = new ScriptLifecycleManager();

                // Global variable delegates - bridge from ExecutionContext.GlobalVariables
                var globalVars = parameters.Context?.GlobalVariables;
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
                    parameters.Context?.GlobalVariables,
                    parameters.NodeResults);

                // ★ 捕获解析后的输入值（供调试监视面板显示）
                parameters.ResolvedInputValues = inputValues;

                if (parameters.Inputs.Count > 0)
                {
                    parameters.LogInfo($"  解析了 {inputValues.Count} 个输入绑定: " +
                        $"全局变量可用={parameters.Context?.GlobalVariables?.Count ?? 0}, " +
                        $"上游节点结果={parameters.NodeResults?.Count ?? 0}");
                }

                var errors = lifecycleManager.Execute(
                    parameters.GeneratedCode,
                    parameters.ScriptCode,
                    parameters.AssemblyReferenceFullPath,
                    getGlobal,
                    setGlobal,
                    inputValues);

                result.CompileErrors = errors;
                result.CompileTimeMs = lifecycleManager.CompileTimeMs;
                result.ExecutionTimeMs = lifecycleManager.ExecutionTimeMs;
                result.LogMessages = lifecycleManager.LogMessages.ToList();

                foreach (var kvp in lifecycleManager.OutputValues)
                {
                    result.OutputValues[kvp.Key] = kvp.Value;
                }

                if (errors.Count == 0)
                {
                    result.SetSuccess(lifecycleManager.ExecutionTimeMs);
                    parameters.LogSuccess(
                        $"Script complete | Compile: {lifecycleManager.CompileTimeMs}ms | Exec: {lifecycleManager.ExecutionTimeMs}ms | " +
                        $"Outputs: {result.OutputValues.Count} | Logs: {result.LogMessages.Count}");
                }
                else
                {
                    var errorMsg = string.Join("; ", errors.Take(3));
                    if (errors.Count > 3) errorMsg += "...";
                    parameters.LogError($"Script failed: {errorMsg}");
                    result.SetError(errorMsg);
                }
            }
            catch (Exception ex)
            {
                parameters.LogError($"Script exception: {ex.Message}", ex);
                result.SetError($"Script exception: {ex.Message}", ex);
            }

            return result;
        }

        public FrameworkElement? CreateDebugControl(IToolPlugin tool)
        {
            return new ScriptDebugControl();
        }
    }
}
