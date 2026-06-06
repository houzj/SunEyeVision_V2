using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Tool.Subroutine.Models
{
    /// <summary>
    /// 子程序执行结果 — 子工作流中所有节点的输出汇总。
    /// 下游工具可通过 AvailableDataSource 绑定此结果中的属性。
    /// </summary>
    public class SubroutineResults : ToolResults
    {
        /// <summary>各节点输出结果（节点ID → 结果对象）</summary>
        public Dictionary<string, object> NodeOutputs { get; set; } = new();

        /// <summary>子工作流执行耗时(ms)</summary>
        public long SubWorkflowExecutionTimeMs { get; set; }

        /// <summary>执行的节点数</summary>
        public int ExecutedNodeCount { get; set; }

        /// <summary>执行的时间戳</summary>
        public DateTime ExecutedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 解析后的输出端口值（端口名 → 值），由 SubroutineTool.Run() 在子工作流执行后填充。
        /// 下游工具可通过 AvailableDataSource 绑定这些属性，与其他工具的输出属性一致。
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, object> ResolvedOutputValues { get; set; } = new();

        public override Dictionary<string, IVisionImage?> GetDisplayImages()
        {
            var images = new Dictionary<string, IVisionImage?>();
            foreach (var kvp in NodeOutputs)
            {
                if (kvp.Value is IVisionImage img && !img.IsEmpty)
                    images[kvp.Key] = img;
            }
            return images;
        }

        public override IReadOnlyList<ResultItem> GetResultItems()
        {
            var items = new List<ResultItem>
            {
                ResultItem.Numeric("执行节点数", ExecutedNodeCount),
                ResultItem.Numeric("子流程耗时(ms)", SubWorkflowExecutionTimeMs),
                ResultItem.Boolean("执行成功", IsSuccess)
            };
            foreach (var kvp in ResolvedOutputValues)
                items.Add(new ResultItem { Name = kvp.Key, Value = kvp.Value, Type = InferResultItemType(kvp.Value) });
            return items;
        }

        private static ResultItemType InferResultItemType(object? value) => value switch
        {
            double or int or long or float => ResultItemType.Numeric,
            bool => ResultItemType.Boolean,
            string => ResultItemType.Text,
            _ => ResultItemType.Object
        };

        public override List<PropertyMapEntry> GetPropertyMap()
        {
            var map = new List<PropertyMapEntry>
            {
                new("SubWorkflowExecutionTimeMs", "子流程耗时", "执行信息"),
                new("ExecutedNodeCount", "执行节点数", "执行信息"),
                new("ExecutedAt", "执行时间", "执行信息")
            };
            foreach (var kvp in ResolvedOutputValues)
                map.Add(new PropertyMapEntry(kvp.Key, kvp.Key, "输出结果"));
            return map;
        }

        /// <summary>
        /// 转换为字典（与 BlobAnalysisResults 一致）
        /// </summary>
        public override Dictionary<string, object> ToDictionary()
        {
            var dict = base.ToDictionary();
            foreach (var kvp in ResolvedOutputValues)
                dict[kvp.Key] = kvp.Value;
            return dict;
        }
    }
}
