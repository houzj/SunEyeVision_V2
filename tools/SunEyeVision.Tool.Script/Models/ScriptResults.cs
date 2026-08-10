using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.Script.Models
{
    /// <summary>
    /// 脚本工具执行结果 — 包含输出值、日志、编译错误等信息
    /// </summary>
    public class ScriptResults : ToolResults
    {
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
                var sysType = SdkTypeVocabulary.ToSystemType(cfg.TypeName)
                    ?? throw new InvalidOperationException(
                        $"脚本输出变量 '{cfg.Name}' 类型 '{cfg.TypeName}' 未在 SdkTypeVocabulary 词汇表注册。");
                var rawValue = OutputValues.TryGetValue(cfg.Name, out var v) ? v : null;
                variables.Add(new OutputVariable
                {
                    Name = cfg.Name,
                    VariableType = cfg.TypeName,
                    DisplayName = cfg.Name,
                    Group = cfg.Group,
                    Kind = OutputVariableKind.Data,
                    RuntimeValue = OutputVariableArrayPromoter.Promote(rawValue, cfg.TypeName, sysType)
                });
            }
            return variables;
        }

    }
}