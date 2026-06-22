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

                // Global variable delegates - injected by WorkflowExecutionEngine in workflow mode
                Func<string, object?>? getGlobal = null;
                Action<string, object?>? setGlobal = null;

                // ★ 解析输入绑定（集成时由 WorkflowEngine 提供，此处为空）
                Dictionary<string, object?>? inputValues = null;
                if (parameters.Inputs.Count > 0)
                {
                    inputValues = new Dictionary<string, object?>();
                    foreach (var input in parameters.Inputs)
                    {
                        // 调试模式或未集成 Workflow 时，输入为空值
                        inputValues[input.Name] = null;
                    }
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

        public FrameworkElement? CreateDebugControl()
        {
            return new ScriptDebugControl();
        }
    }
}
