using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;

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

        public override IReadOnlyList<ResultItem> GetResultItems()
        {
            var items = new List<ResultItem>();
            foreach (var kvp in OutputValues)
            {
                items.Add(new ResultItem
                {
                    Name = kvp.Key,
                    Value = kvp.Value,
                    Type = GetResultItemType(kvp.Value)
                });
            }
            return items;
        }

        private static ResultItemType GetResultItemType(object? value)
        {
            if (value == null) return ResultItemType.Object;
            return value switch
            {
                int or long or float or double or decimal => ResultItemType.Numeric,
                bool => ResultItemType.Boolean,
                string => ResultItemType.Text,
                _ => ResultItemType.Object
            };
        }
    }
}