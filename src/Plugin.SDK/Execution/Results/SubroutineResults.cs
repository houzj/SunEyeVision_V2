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

        /// <summary>
        /// 获取可显示的图像输出。
        /// 只收集用户声明的 Image 类型输出（ResolvedOutputValues 中的真实值），
        /// 而非遍历 NodeOutputs（其存的是内部节点结果对象，永远不可能是 IVisionImage）。
        /// 对齐输出链路：图像只收用户声明的 Image 输出，不自动吞内部节点全部图像。
        /// </summary>
        public override Dictionary<string, IVisionImage?> GetDisplayImages()
        {
            var images = new Dictionary<string, IVisionImage?>();
            foreach (var kvp in ResolvedOutputValues)
                if (kvp.Value is IVisionImage img && !img.IsEmpty)
                    images[kvp.Key] = img;
            return images;
        }

        public override List<PropertyMapEntry> GetPropertyMap()
        {
            var map = new List<PropertyMapEntry>
            {
                new("SubWorkflowExecutionTimeMs", "\u5b50\u6d41\u7a0b\u8017\u65f6", "\u6267\u884c\u4fe1\u606f"),
                new("ExecutedNodeCount", "\u6267\u884c\u8282\u70b9\u6570", "\u6267\u884c\u4fe1\u606f")
            };
            var keys = ResolvedOutputValues.Count > 0 ? ResolvedOutputValues.Keys : OutputConfigs.Select(c => c.Name);
            foreach (var name in keys)
                map.Add(new PropertyMapEntry(name, name, "\u8f93\u51fa\u7ed3\u679c"));
            return map;
        }

        /// <summary>
        /// 基于输出变量契约派生：保留执行元信息（中文本地化），并叠加用户声明的输出变量
        /// （含 DisplayName/Group 中文别名与分组，运行值来自 ResolvedOutputValues）。
        /// </summary>
        public override IReadOnlyList<OutputVariable> GetOutputVariables()
        {
            var variables = new List<OutputVariable>();

            // 元信息：GetPropertyMap 中存在真实 CLR 属性的条目（如子流程耗时）
            foreach (var entry in GetPropertyMap())
            {
                var prop = GetType().GetProperty(entry.PropertyName);
                if (prop == null) continue; // 跳过动态输出变量（无 CLR 属性）

                var variableType = SdkTypeVocabulary.ToTypeVocabulary(prop.PropertyType)
                    ?? throw new InvalidOperationException(
                        $"子程序元信息属性 '{entry.PropertyName}' 类型 '{prop.PropertyType.Name}' 未注册到 SdkTypeVocabulary 词汇表。");
                variables.Add(new OutputVariable
                {
                    Name = entry.PropertyName,
                    VariableType = variableType,
                    DisplayName = entry.DisplayName,
                    Group = entry.GroupName,
                    Kind = OutputVariableKind.Data,
                    RuntimeValue = OutputVariableArrayPromoter.Promote(prop.GetValue(this), variableType, prop.PropertyType)
                });
            }

            // 用户声明输出变量（含 DisplayName/Group）
            foreach (var cfg in OutputConfigs)
            {
                var sysType = SdkTypeVocabulary.ToSystemType(cfg.TypeName);
                if (sysType is null)
                {
                    throw new InvalidOperationException(
                        $"子程序输出变量 '{cfg.Name}' 类型 '{cfg.TypeName}' 未在 SdkTypeVocabulary 词汇表注册。");
                }
                var rawValue = ResolvedOutputValues.TryGetValue(cfg.Name, out var v) ? v : null;
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

        public override Dictionary<string, object> ToDictionary()
        {
            var dict = base.ToDictionary();
            foreach (var kvp in ResolvedOutputValues)
                dict[kvp.Key] = kvp.Value;
            return dict;
        }
    }
}
