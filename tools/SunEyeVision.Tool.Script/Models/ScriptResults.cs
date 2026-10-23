using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 脚本工具执行结果 — 包含输出值、日志、编译错误等信息
    /// </summary>
    public class ScriptResults : ToolResults, IDisposable
    {
        internal void OwnInputs(IDisposable inputs)
        {
            var outputs = Resources;
            Resources = new ResultResourceOwner(() =>
            {
                try { outputs?.Dispose(); }
                finally { inputs.Dispose(); }
            });
        }
        internal void OwnOutputs(ScriptEngine.ScriptOutputSnapshot? outputs)
        {
            if (outputs != null) Resources = new ResultResourceOwner(() =>
            {
                try { outputs.Dispose(); }
                finally { OutputValues.Clear(); }
            });
        }
        public void Dispose()
        {
            if (Resources != null) Resources.Dispose();
            else OutputValues.Clear();
        }
        /// <summary>
        /// 脚本设置的输出值字典（通过 SetOutput 设置）
        /// </summary>
        public Dictionary<string, object?> OutputValues { get; set; } = new();

        /// <summary>输出变量声明（来自 ScriptParameters.Outputs，通过 Run 注入），作为输出变量契约 SSOT</summary>
        public List<ScriptOutputConfig> OutputConfigs { get; set; } = new();

        /// <summary>
        /// 执行过程中的日志信息列表
        /// </summary>
        public List<string> LogMessages { get; set; } = new();

        /// <summary>
        /// 编译错误列表
        /// </summary>
        public List<string> CompileErrors { get; set; } = new();

        /// <summary>
        /// 编译耗时（毫秒）
        /// </summary>
        public long CompileTimeMs { get; set; }

        /// <summary>
        /// 执行的脚本代码（供调试显示）
        /// </summary>
        public string? ScriptCode { get; set; }

        /// <summary>
        /// 基于输出变量契约派生：变量声明来自 OutputConfigs，运行值来自 OutputValues。
        /// 未知类型（未注册到 SdkTypeVocabulary）直接报错（rule-015）。
        /// </summary>
        public override IReadOnlyList<OutputVariable> GetOutputVariables()
        {
            var variables = new List<OutputVariable>();
            foreach (var cfg in OutputConfigs)
            {
                var sysType = ScriptIoTypeDescriptor.From(cfg.TypeName).ClrType;
                var rawValue = OutputValues.TryGetValue(cfg.Name, out var v) ? v : null;
                variables.Add(new OutputVariable
                {
                    Name = cfg.Name,
                    FieldId = cfg.FieldId, DeclaredType = sysType,
                    IsCollection = cfg.IsAccumulated || sysType.IsArray,
                    FixedCount = cfg.IsAccumulated || sysType.IsArray ? null : 1,
                    VariableType = cfg.TypeName,
                    DisplayName = cfg.Name,
                    Group = cfg.Group,
                    Kind = OutputVariableKind.Data,
                    RuntimeValue = OutputVariableArrayPromoter.Promote(rawValue, cfg.TypeName, sysType)
                });
            }

            // 统一附加执行耗时（工具执行信息，固定归入"执行信息"分组，置顶展示）
            variables.Add(new OutputVariable
            {
                Name = "ExecutionTimeMs",
                VariableType = "Double",
                DisplayName = "执行耗时",
                Group = "执行信息",
                Kind = OutputVariableKind.Data,
                RuntimeValue = ExecutionTimeMs
            });

            // 统一附加节点状态（基类统一下沉；本类 GetOutputVariables 完全 override 不合并基类，须显式附加）
            variables.Add(new OutputVariable
            {
                Name = nameof(IsPass),
                VariableType = "Boolean",
                DisplayName = "节点状态",
                Group = "执行信息",
                Kind = OutputVariableKind.Data,
                RuntimeValue = IsPass
            });

            return variables;
        }

    }
}
