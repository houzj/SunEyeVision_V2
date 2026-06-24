using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Results
{
    public class SubroutineResults : ToolResults
    {
        public Dictionary<string, object> NodeOutputs { get; set; } = new();
        public long SubWorkflowExecutionTimeMs { get; set; }
        public int ExecutedNodeCount { get; set; }
        public DateTime ExecutedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public Dictionary<string, object> ResolvedOutputValues { get; set; } = new();

        [JsonIgnore]
        public List<SubroutineOutputConfig> OutputConfigs { get; set; } = new();

        public override Dictionary<string, IVisionImage?> GetDisplayImages()
        {
            var images = new Dictionary<string, IVisionImage?>();
            foreach (var kvp in NodeOutputs)
                if (kvp.Value is IVisionImage img && !img.IsEmpty)
                    images[kvp.Key] = img;
            return images;
        }

        public override IReadOnlyList<ResultItem> GetResultItems()
        {
            var items = new List<ResultItem>
            {
                ResultItem.Numeric("\u5b50\u6d41\u7a0b\u8017\u65f6(ms)", SubWorkflowExecutionTimeMs),
                ResultItem.Numeric("\u6267\u884c\u8282\u70b9\u6570", ExecutedNodeCount),
                ResultItem.Boolean("\u6267\u884c\u6210\u529f", IsSuccess)
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
                new("SubWorkflowExecutionTimeMs", "\u5b50\u6d41\u7a0b\u8017\u65f6", "\u6267\u884c\u4fe1\u606f"),
                new("ExecutedNodeCount", "\u6267\u884c\u8282\u70b9\u6570", "\u6267\u884c\u4fe1\u606f"),
                new("ExecutedAt", "\u6267\u884c\u65f6\u95f4", "\u6267\u884c\u4fe1\u606f")
            };
            var keys = ResolvedOutputValues.Count > 0 ? ResolvedOutputValues.Keys : OutputConfigs.Select(c => c.Name);
            foreach (var name in keys)
                map.Add(new PropertyMapEntry(name, name, "\u8f93\u51fa\u7ed3\u679c"));
            return map;
        }

        public override Dictionary<string, object> ToDictionary()
        {
            var dict = base.ToDictionary();
            foreach (var kvp in ResolvedOutputValues)
                dict[kvp.Key] = kvp.Value;
            return dict;
        }
    }
}
