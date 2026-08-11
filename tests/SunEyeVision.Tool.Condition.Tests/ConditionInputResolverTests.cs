using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Tool.Condition.Models;
using Xunit;

namespace SunEyeVision.Tool.Condition.Tests
{
    /// <summary>
    /// 条件分支工具解析逻辑单元测试（方案验收 UT-COND-01~10）。
    /// 覆盖：ConditionInputResolver 模式判定契约（C3）+ 常量转换 + ConditionTool.Run 全分支。
    /// </summary>
    public class ConditionInputResolverTests
    {
        // 合法 GUID 节点ID（与 WorkflowNode.Id 格式一致）
        private const string NodeA = "e2a8c5d7-f3e2-4b1a-9c5d-8f7e6b5a4c3d";
        private const string NodeB = "11111111-2222-4333-8444-555555555555";

        #region UT-COND-08 模式判定契约：常量（含点/无点不误判绑定）

        [Fact]
        public void Constant_WithDot_NotGuid_IsConstantMode()
        {
            // "3.5" 含点但节点段 "3" 不是 GUID → 常量模式，按 Double 解析
            var value = ConditionInputResolver.Resolve("3.5", "Double", "int0", null);
            Assert.Equal(3.5, value);
        }

        [Fact]
        public void Constant_NoDot_IsConstantMode()
        {
            var value = ConditionInputResolver.Resolve("123", "Int", "int0", null);
            Assert.Equal(123, value);
        }

        [Fact]
        public void Constant_String_IsLiteral()
        {
            var value = ConditionInputResolver.Resolve("OK", "String", "string0", null);
            Assert.Equal("OK", value);
        }

        #endregion

        #region UT-COND-03 常量解析参与判断

        [Fact]
        public void IntConstant_WithinRange_Matches()
        {
            var parameters = CreateParameters(rules: new[]
            {
                new ConditionRule
                {
                    RuleType = "Int",
                    Name = "int0",
                    InputText = "5",
                    MinValue = 0,
                    MaxValue = 10,
                    Judgment = "OK",
                    ExecutionModuleId = "TargetNode1"
                }
            });

            var result = new ConditionTool().Run(parameters);
            Assert.True(result.IsMatched);
            Assert.Equal("TargetNode1", result.NextNodeId);
            Assert.Equal("int0", result.MatchedRuleName);
        }

        [Fact]
        public void IntConstant_OutOfRange_DoesNotMatch()
        {
            var parameters = CreateParameters(rules: new[]
            {
                new ConditionRule
                {
                    RuleType = "Int",
                    Name = "int0",
                    InputText = "50",
                    MinValue = 0,
                    MaxValue = 10,
                    Judgment = "OK",
                    ExecutionModuleId = "TargetNode1"
                }
            });

            var result = new ConditionTool().Run(parameters);
            Assert.False(result.IsMatched);
            Assert.Null(result.NextNodeId);
        }

        [Fact]
        public void NGJudgment_OutOfRange_Matches()
        {
            var parameters = CreateParameters(rules: new[]
            {
                new ConditionRule
                {
                    RuleType = "Double",
                    Name = "float0",
                    InputText = "3.5",
                    MinValue = 0,
                    MaxValue = 1,
                    Judgment = "NG",
                    ExecutionModuleId = "TargetNode1"
                }
            });

            var result = new ConditionTool().Run(parameters);
            Assert.True(result.IsMatched);
            Assert.Equal("TargetNode1", result.NextNodeId);
        }

        #endregion

        #region UT-COND-02 全部未命中 → 流程终止

        [Fact]
        public void NoRuleMatches_ReturnsNull_NextNodeId()
        {
            var parameters = CreateParameters(rules: new[]
            {
                new ConditionRule
                {
                    RuleType = "Int",
                    Name = "int0",
                    InputText = "99",
                    MinValue = 0,
                    MaxValue = 10,
                    Judgment = "OK",
                    ExecutionModuleId = "TargetNode1"
                },
                new ConditionRule
                {
                    RuleType = "Int",
                    Name = "int1",
                    InputText = "99",
                    MinValue = 0,
                    MaxValue = 10,
                    Judgment = "OK",
                    ExecutionModuleId = "TargetNode2"
                }
            });

            var result = new ConditionTool().Run(parameters);
            Assert.False(result.IsMatched);
            Assert.Null(result.NextNodeId);
        }

        #endregion

        #region UT-COND-04 绑定表达式取上游值

        [Fact]
        public void Binding_ResolvesUpstreamValue()
        {
            // 构造上游节点结果：输出变量 "Area" = 150
            var upstream = new GenericToolResults { ToolName = "Blob分析" };
            upstream.AddResultItem("Area", 150.0);

            var nodeResults = new Dictionary<string, ToolResults>
            {
                [NodeA] = upstream
            };

            var parameters = CreateParameters(
                nodeResults: nodeResults,
                rules: new[]
                {
                    new ConditionRule
                    {
                        RuleType = "Double",
                        Name = "float0",
                        InputText = $"{NodeA}.Area",
                        MinValue = 100,
                        MaxValue = 200,
                        Judgment = "OK",
                        ExecutionModuleId = "TargetNode1"
                    }
                });

            var result = new ConditionTool().Run(parameters);
            Assert.True(result.IsMatched);
            Assert.Equal("TargetNode1", result.NextNodeId);
        }

        [Fact]
        public void Binding_UpstreamNotExecuted_ReportsNodeMissing()
        {
            // 绑定未执行节点：NodeResults 不含该节点 → 报"不在执行结果中"而非误判常量
            var parameters = CreateParameters(
                nodeResults: new Dictionary<string, ToolResults>(),
                rules: new[]
                {
                    new ConditionRule
                    {
                        RuleType = "Double",
                        Name = "float0",
                        InputText = $"{NodeA}.Area",
                        MinValue = 100,
                        MaxValue = 200,
                        Judgment = "OK",
                        ExecutionModuleId = "TargetNode1"
                    }
                });

            var ex = Assert.Throws<InvalidOperationException>(() => new ConditionTool().Run(parameters));
            Assert.Contains("不在执行结果中", ex.Message);
        }

        #endregion

        #region UT-COND-05 string 规则：有值即命中

        [Fact]
        public void StringRule_WithUpstreamValue_Matches()
        {
            var upstream = new GenericToolResults { ToolName = "缺陷检测" };
            upstream.AddResultItem("类型", "划痕");

            var nodeResults = new Dictionary<string, ToolResults>
            {
                [NodeA] = upstream
            };

            var parameters = CreateParameters(
                nodeResults: nodeResults,
                rules: new[]
                {
                    new ConditionRule
                    {
                        RuleType = "String",
                        Name = "string0",
                        InputText = $"{NodeA}.类型",
                        ExecutionModuleId = "TargetNode1"
                    }
                });

            var result = new ConditionTool().Run(parameters);
            Assert.True(result.IsMatched);
            Assert.Equal("TargetNode1", result.NextNodeId);
        }

        #endregion

        #region UT-COND-10 常量解析失败 → 抛异常带原文

        [Fact]
        public void IntConstant_ParseFailure_ThrowsWithOriginal()
        {
            var parameters = CreateParameters(rules: new[]
            {
                new ConditionRule
                {
                    RuleType = "Int",
                    Name = "int0",
                    InputText = "abc",
                    MinValue = 0,
                    MaxValue = 10,
                    Judgment = "OK",
                    ExecutionModuleId = "TargetNode1"
                }
            });

            var ex = Assert.Throws<InvalidOperationException>(() => new ConditionTool().Run(parameters));
            Assert.Contains("abc", ex.Message);
        }

        [Fact]
        public void EmptyInput_Throws()
        {
            var parameters = CreateParameters(rules: new[]
            {
                new ConditionRule
                {
                    RuleType = "String",
                    Name = "string0",
                    InputText = "",
                    ExecutionModuleId = "TargetNode1"
                }
            });

            Assert.Throws<InvalidOperationException>(() => new ConditionTool().Run(parameters));
        }

        #endregion

        #region UT-COND-01 多规则顺序评估：命中第一条即返回

        [Fact]
        public void MultipleRules_FirstMatch_Wins()
        {
            var parameters = CreateParameters(rules: new[]
            {
                new ConditionRule
                {
                    RuleType = "Int",
                    Name = "int0",
                    InputText = "5",
                    MinValue = 0,
                    MaxValue = 10,
                    Judgment = "OK",
                    ExecutionModuleId = "TargetNode1"
                },
                new ConditionRule
                {
                    RuleType = "Int",
                    Name = "int1",
                    InputText = "5",
                    MinValue = 0,
                    MaxValue = 10,
                    Judgment = "OK",
                    ExecutionModuleId = "TargetNode2"
                }
            });

            var result = new ConditionTool().Run(parameters);
            Assert.True(result.IsMatched);
            Assert.Equal("TargetNode1", result.NextNodeId);
            Assert.Equal("int0", result.MatchedRuleName);
        }

        #endregion

        #region 辅助方法

        private static ConditionParameters CreateParameters(
            IReadOnlyList<ConditionRule>? rules = null,
            IDictionary<string, ToolResults>? nodeResults = null)
        {
            var parameters = new ConditionParameters();
            if (rules != null)
            {
                foreach (var rule in rules)
                {
                    parameters.Rules.Add(rule);
                }
            }
            parameters.NodeResults = nodeResults ?? new Dictionary<string, ToolResults>();
            return parameters;
        }

        #endregion
    }
}
