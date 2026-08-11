using System;
using System.Text.Json;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.Condition.Models;
using Xunit;

namespace SunEyeVision.Tool.Condition.Tests
{
    /// <summary>
    /// 序列化往返契约测试 — 锁定规则条目的 7 字段 + 集合数量保存→加载完整恢复。
    /// 覆盖最终方案验收 #5：任一字段保存丢失立刻红灯。
    /// </summary>
    public class ConditionSerializationContractTests
    {
        // 合法 GUID 节点ID（与 WorkflowNode.Id 格式一致）
        private const string NodeA = "e2a8c5d7-f3e2-4b1a-9c5d-8f7e6b5a4c3d";
        private const string TargetNode1 = "22222222-3333-4444-8555-666666666666";

        private static JsonSerializerOptions GetSerializationOptions()
        {
            // 注册 ConditionParameters 到多态配置（与 ToolRegistry.RegisterTool 运行期行为一致）
            ParameterTypeRegistry.RegisterParameterType(typeof(ConditionParameters), "ConditionParameters");
            return ParameterTypeRegistry.SerializationOptions;
        }

        /// <summary>
        /// 构造带完整 7 字段 + 绑定表达式条件的规则。
        /// 绑定表达式 InputText = "节点GUID.属性名"（单轨真相源，契约 C1）。
        /// </summary>
        private static ConditionParameters CreateFullParameters()
        {
            var parameters = new ConditionParameters();
            parameters.Rules.Add(new ConditionRule
            {
                Name = "float0",
                RuleType = "Double",
                InputText = $"{NodeA}.面积",
                MinValue = 1.5,
                MaxValue = 999.75,
                Judgment = "NG",
                ExecutionModuleId = TargetNode1
            });
            parameters.Rules.Add(new ConditionRule
            {
                Name = "string0",
                RuleType = "String",
                InputText = "OK",
                MinValue = 0,
                MaxValue = 1000,
                Judgment = "OK",
                ExecutionModuleId = TargetNode1
            });
            return parameters;
        }

        #region 验收 #5：7 字段 + 集合数量往返全等

        [Fact]
        public void RoundTrip_PreservesAllSevenFields_AndRuleCount()
        {
            var options = GetSerializationOptions();
            var source = CreateFullParameters();

            var json = JsonSerializer.Serialize(source, options);
            var back = JsonSerializer.Deserialize<ConditionParameters>(json, options);

            Assert.NotNull(back);
            Assert.Equal(source.Rules.Count, back!.Rules.Count);

            // 规则1：Double + 绑定表达式（全字段）
            var r1 = back.Rules[0];
            Assert.Equal("float0", r1.Name);
            Assert.Equal("Double", r1.RuleType);
            Assert.Equal($"{NodeA}.面积", r1.InputText);   // 绑定表达式原样保留（单轨契约 C1）
            Assert.Equal(1.5, r1.MinValue);
            Assert.Equal(999.75, r1.MaxValue);
            Assert.Equal("NG", r1.Judgment);
            Assert.Equal(TargetNode1, r1.ExecutionModuleId);

            // 规则2：String + 常量（全字段默认值亦恢复）
            var r2 = back.Rules[1];
            Assert.Equal("string0", r2.Name);
            Assert.Equal("String", r2.RuleType);
            Assert.Equal("OK", r2.InputText);
            Assert.Equal(0, r2.MinValue);
            Assert.Equal(1000, r2.MaxValue);
            Assert.Equal("OK", r2.Judgment);
            Assert.Equal(TargetNode1, r2.ExecutionModuleId);
        }

        #endregion

        #region 契约：序列化含 $type 鉴别符 + Rules 落盘

        [Fact]
        public void Serialize_ContainsTypeDiscriminator_AndRulesArray()
        {
            var options = GetSerializationOptions();
            var source = CreateFullParameters();

            var json = JsonSerializer.Serialize(source, options);

            // $type 鉴别符（rule-010 多态契约；WriteIndented 使冒号后有空格）
            Assert.Contains("\"$type\": \"ConditionParameters\"", json);
            // Rules 集合必须落盘（否则持久化丢失规则内容）
            Assert.Contains("\"Rules\"", json);
            // 绑定表达式必须以 GUID.属性名 原文落盘（非中文显示名）
            Assert.Contains($"{NodeA}.面积", json);
        }

        #endregion

        #region 契约：反序列化类型正确恢复（不静默落到 GenericToolParameters）

        [Fact]
        public void Deserialize_ReturnsConditionParameters_NotGenericFallback()
        {
            var options = GetSerializationOptions();
            var source = CreateFullParameters();
            var json = JsonSerializer.Serialize(source, options);

            // 以 ToolParameters 基类句柄反序列化 → 必须是 ConditionParameters（多态 $type 生效）
            var asBase = JsonSerializer.Deserialize<ToolParameters>(json, options);

            Assert.NotNull(asBase);
            Assert.IsType<ConditionParameters>(asBase);
            Assert.Equal(2, ((ConditionParameters)asBase!).Rules.Count);
        }

        #endregion
    }
}
