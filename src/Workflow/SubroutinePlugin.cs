using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Logging;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Metadata;
using LogLevel = SunEyeVision.Plugin.SDK.Logging.LogLevel;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 子程序插件实现
    /// </summary>
    public class SubroutinePlugin : IWorkflowControlPlugin
    {
        private readonly WorkflowEngine _workflowEngine;

        public SubroutinePlugin(WorkflowEngine workflowEngine)
        {
            _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        }

        #region IWorkflowControlPlugin 实现

        /// <summary>
        /// 获取插件元数据
        /// </summary>
        public ToolMetadata GetMetadata()
        {
            return new ToolMetadata
            {
                Id = "WorkflowControl",
                DisplayName = "工作流控制插件",
                Description = "提供子程序调用和条件判断功能",
                Category = "WorkflowControl"
            };
        }

        /// <summary>
        /// 获取工作流控制节点列表
        /// </summary>
        public List<WorkflowControlNode> GetWorkflowControlNodes()
        {
            return new List<WorkflowControlNode>
            {
                new SubroutineNode(),
                new ConditionNode()
            };
        }

        /// <summary>
        /// 创建子程序节点
        /// </summary>
        /// <param name="nodeName">节点名称（包含全局序号，用于序列化）</param>
        /// <param name="dispName">节点显示名称（不含序号，用于UI显示）</param>
        /// <param name="workflowId">子程序工作流ID</param>
        /// <returns>创建的SubroutineNode</returns>
        public SubroutineNode CreateSubroutineNode(string nodeName, string dispName, string workflowId)
        {
            var node = new SubroutineNode(
                Guid.NewGuid().ToString(),
                nodeName,
                dispName);
            node.SubroutineId = workflowId;
            node.SubroutineName = nodeName;
            return node;
        }

        /// <summary>
        /// 创建条件判断节点
        /// </summary>
        /// <param name="nodeName">节点名称（包含全局序号，用于序列化）</param>
        /// <param name="dispName">节点显示名称（不含序号，用于UI显示）</param>
        /// <param name="conditionExpression">条件表达式</param>
        /// <returns>创建的ConditionNode</returns>
        public ConditionNode CreateConditionNode(string nodeName, string dispName, string conditionExpression)
        {
            var node = new ConditionNode(
                Guid.NewGuid().ToString(),
                nodeName,
                dispName);
            node.SetExpressionCondition(conditionExpression);
            return node;
        }

        /// <summary>
        /// 执行子程序
        /// </summary>
        public async Task<ExecutionResult> ExecuteSubroutine(
            SubroutineNode node,
            WorkflowContext context)
        {
            var result = new ExecutionResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 记录调用信息
                context.PushCallInfo(new SubroutineCallInfo
                {
                    SubroutineId = node.SubroutineId,
                    NodeId = node.Id,
                    CallDepth = context.GetCurrentCallDepth(),
                    CallTime = DateTime.Now,
                    InputParameters = MapInputParameters(node, context),
                    Status = CallStatus.Calling
                });

                // 获取子程序工作流
                var subroutineWorkflow = _workflowEngine.GetWorkflow(node.SubroutineId);
                if (subroutineWorkflow == null)
                {
                    result.AddError($"子程序 {node.SubroutineId} 不存在", node.Id);
                    return result;
                }

                // 验证节点配置
                var validationResult = node.Validate();
                if (!validationResult.IsValid)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        result.AddError(error, node.Id);
                    }
                    return result;
                }

                // 更新调用状态
                var currentCallInfo = context.GetCurrentCallInfo();
                if (currentCallInfo != null)
                {
                    currentCallInfo.Status = CallStatus.Executing;
                }

                // 执行子程序
                if (!node.IsLoop)
                {
                    // 单次执行
                    var execResult = await ExecuteSubroutineOnce(node, subroutineWorkflow, context);
                    result.Merge(execResult);
                }
                else
                {
                    // 循环执行
                    result = await ExecuteSubroutineWithLoop(node, subroutineWorkflow, context);
                }

                // 映射输出参数
                MapOutputParameters(node, result, context);

                // 记录执行路径
                context.AddExecutionPathItem(new ExecutionPathItem
                {
                    NodeId = node.Id,
                    NodeType = "Subroutine",
                    Timestamp = DateTime.Now,
                    Duration = stopwatch.Elapsed,
                    Success = result.Success
                });

                // 更新调用状态
                if (currentCallInfo != null)
                {
                    currentCallInfo.Status = result.Success ? CallStatus.Completed : CallStatus.Failed;
                    currentCallInfo.OutputResults = new Dictionary<string, object>(result.Outputs);
                    if (!result.Success)
                    {
                        currentCallInfo.Error = string.Join("; ", result.Errors);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                result.IsStopped = true;
            }
            catch (Exception ex)
            {
                result.AddError($"子程序执行失败: {ex.Message}", node.Id);
            }
            finally
            {
                stopwatch.Stop();
                result.ExecutionTime = stopwatch.Elapsed;
                context.PopCallInfo();
            }

            return result;
        }

        /// <summary>
        /// 评估条件表达式
        /// </summary>
        public bool EvaluateCondition(ConditionNode node, WorkflowContext context)
        {
            try
            {
                if (node.ConditionType == ConditionType.Simple)
                {
                    return EvaluateSimpleCondition(node, context);
                }
                else
                {
                    return EvaluateExpressionCondition(node, context);
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 单次执行子程序
        /// </summary>
        private async Task<ExecutionResult> ExecuteSubroutineOnce(
            SubroutineNode node,
            Workflow subroutineWorkflow,
            WorkflowContext context)
        {
            var source = LogSource.Runtime(context.WorkflowName ?? "未知", node.SubroutineName);
            context.AddLog($"执行子程序", LogLevel.Info, source);

            // 简化实现：直接执行工作流
            var inputImage = GetInputImageFromContext(context);
            var results = _workflowEngine.ExecuteWorkflow(node.SubroutineId, inputImage);

            var result = new ExecutionResult { Success = true };

            if (results != null && results.Count > 0)
            {
                foreach (var algorithmResult in results)
                {
                    if (algorithmResult.Success)
                    {
                        result.Outputs[$"Result_{algorithmResult.AlgorithmName}"] = algorithmResult.ResultImage;
                    }
                    else
                    {
                        result.AddError($"算法 {algorithmResult.AlgorithmName} 执行失败", node.Id);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 循环执行子程序
        /// </summary>
        private async Task<ExecutionResult> ExecuteSubroutineWithLoop(
            SubroutineNode node,
            Workflow subroutineWorkflow,
            WorkflowContext context)
        {
            var result = new ExecutionResult();
            node.CurrentIteration = 0;

            var source = LogSource.Runtime(context.WorkflowName ?? "未知", node.SubroutineName);
            context.AddLog($"开始循环执行, 最大次数: {node.MaxIterations}", LogLevel.Info, source);

            for (node.CurrentIteration = 0;
                 node.CurrentIteration < node.MaxIterations;
                 node.CurrentIteration++)
            {
                // 检查取消令牌
                if (context.IsCancellationRequested())
                {
                    result.IsStopped = true;
                    break;
                }

                // 检查循环条件
                if (node.LoopType == LoopType.ConditionBased)
                {
                    if (!EvaluateLoopCondition(node, context))
                    {
                        context.AddLog($"循环条件不满足，终止 (迭代: {node.CurrentIteration})", LogLevel.Info, source);
                        break;
                    }
                }

                // 执行一次迭代
                var iterationResult = await ExecuteSubroutineOnce(node, subroutineWorkflow, context);

                if (!iterationResult.Success)
                {
                    result.AddError($"循环第 {node.CurrentIteration + 1} 次迭代失败", node.Id);
                    break;
                }

                // 累加结果
                result.Merge(iterationResult);

                // 报告进度
                context.ReportProgress(new ExecutionProgress
                {
                    CurrentIteration = node.CurrentIteration + 1,
                    TotalIterations = node.MaxIterations,
                    Message = $"执行循环迭代 {node.CurrentIteration + 1}/{node.MaxIterations}",
                    CurrentNodeId = node.Id
                });
            }

            node.TotalExecutionTime = result.ExecutionTime;
            context.AddLog($"循环完成，总迭代: {node.CurrentIteration}", LogLevel.Success, source);

            return result;
        }

        /// <summary>
        /// 映射输入参数
        /// </summary>
        private Dictionary<string, object> MapInputParameters(
            SubroutineNode node,
            WorkflowContext context)
        {
            var parameters = new Dictionary<string, object>();

            foreach (var mapping in node.InputMappings ?? new List<ParameterMapping>())
            {
                if (context.HasVariable(mapping.ExternalPortId))
                {
                    parameters[mapping.InternalPortId] = context.GetVariable(mapping.ExternalPortId);
                }
                else if (mapping.DefaultValue != null)
                {
                    parameters[mapping.InternalPortId] = mapping.DefaultValue;
                }
            }

            return parameters;
        }

        /// <summary>
        /// 映射输出参数
        /// </summary>
        private void MapOutputParameters(
            SubroutineNode node,
            ExecutionResult result,
            WorkflowContext context)
        {
            foreach (var mapping in node.OutputMappings ?? new List<ParameterMapping>())
            {
                if (result.Outputs.TryGetValue(mapping.InternalPortId, out var value))
                {
                    context.SetVariable(mapping.ExternalPortId, value);
                }
            }
        }

        /// <summary>
        /// 评估简单条件
        /// </summary>
        private bool EvaluateSimpleCondition(ConditionNode node, WorkflowContext context)
        {
            var leftValue = GetOperandValue(node.LeftOperand, context);
            var rightValue = node.RightOperandIsLiteral
                ? ParseLiteralValue(node.RightOperand)
                : GetOperandValue(node.RightOperand, context);

            return CompareValues(leftValue, rightValue, node.Operator);
        }

        /// <summary>
        /// 评估表达式条件
        /// </summary>
        private bool EvaluateExpressionCondition(ConditionNode node, WorkflowContext context)
        {
            var expression = node.ConditionExpression;

            // 替换变量占位符
            foreach (var variable in context.Variables)
            {
                expression = expression.Replace($"${variable.Key}", FormatValue(variable.Value));
            }

            // 简化实现：只支持基本的比较表达式
            // 生产环境应该使用完整的表达式解析器
            try
            {
                return EvaluateSimpleExpression(expression);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 评估循环条件
        /// </summary>
        private bool EvaluateLoopCondition(SubroutineNode node, WorkflowContext context)
        {
            var expression = node.LoopCondition;

            foreach (var variable in context.Variables)
            {
                expression = expression.Replace($"${variable.Key}", FormatValue(variable.Value));
            }

            try
            {
                return EvaluateSimpleExpression(expression);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取操作数值
        /// </summary>
        private object GetOperandValue(string operand, WorkflowContext context)
        {
            if (context.HasVariable(operand))
            {
                return context.GetVariable(operand);
            }
            return null;
        }

        /// <summary>
        /// 解析字面量值
        /// </summary>
        private object ParseLiteralValue(string literal)
        {
            if (bool.TryParse(literal, out var boolValue))
            {
                return boolValue;
            }

            if (int.TryParse(literal, out var intValue))
            {
                return intValue;
            }

            if (double.TryParse(literal, out var doubleValue))
            {
                return doubleValue;
            }

            return literal;
        }

        /// <summary>
        /// 比较值
        /// </summary>
        private bool CompareValues(object left, object right, ComparisonOperator op)
        {
            switch (op)
            {
                case ComparisonOperator.Equal:
                    return object.Equals(left, right);

                case ComparisonOperator.NotEqual:
                    return !object.Equals(left, right);

                case ComparisonOperator.GreaterThan:
                    return CompareNumeric(left, right) > 0;

                case ComparisonOperator.GreaterThanOrEqual:
                    return CompareNumeric(left, right) >= 0;

                case ComparisonOperator.LessThan:
                    return CompareNumeric(left, right) < 0;

                case ComparisonOperator.LessThanOrEqual:
                    return CompareNumeric(left, right) <= 0;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 比较数值
        /// </summary>
        private int CompareNumeric(object left, object right)
        {
            if (left == null || right == null)
                return 0;

            if (left is IComparable comparable)
            {
                return comparable.CompareTo(right);
            }

            return 0;
        }

        /// <summary>
        /// 格式化值
        /// </summary>
        private string FormatValue(object value)
        {
            if (value == null)
                return "null";
            if (value is string s)
                return $"\"{s}\"";
            if (value is bool b)
                return b.ToString().ToLower();
            return value.ToString();
        }

        /// <summary>
        /// 评估简单表达式（简化实现）
        /// </summary>
        private bool EvaluateSimpleExpression(string expression)
        {
            // 简化实现：支持基本的比较表达式
            // 生产环境应该使用System.Linq.Expressions或第三方表达式解析器
            if (expression.Contains("=="))
            {
                var parts = expression.Split(new[] { "==" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    return CompareExpressions(parts[0].Trim(), parts[1].Trim()) == 0;
                }
            }
            else if (expression.Contains("!="))
            {
                var parts = expression.Split(new[] { "!=" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    return CompareExpressions(parts[0].Trim(), parts[1].Trim()) != 0;
                }
            }
            else if (expression.Contains(">"))
            {
                var parts = expression.Split(new[] { ">" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    return CompareExpressions(parts[0].Trim(), parts[1].Trim()) > 0;
                }
            }
            else if (expression.Contains("<"))
            {
                var parts = expression.Split(new[] { "<" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    return CompareExpressions(parts[0].Trim(), parts[1].Trim()) < 0;
                }
            }

            return false;
        }

        /// <summary>
        /// 比较表达式
        /// </summary>
        private int CompareExpressions(string left, string right)
        {
            var leftValue = ParseExpressionValue(left);
            var rightValue = ParseExpressionValue(right);

            return CompareNumeric(leftValue, rightValue);
        }

        /// <summary>
        /// 解析表达式值
        /// </summary>
        private object ParseExpressionValue(string value)
        {
            value = value.Trim();

            if (bool.TryParse(value, out var boolValue))
            {
                return boolValue;
            }

            if (int.TryParse(value, out var intValue))
            {
                return intValue;
            }

            if (double.TryParse(value, out var doubleValue))
            {
                return doubleValue;
            }

            return value.Trim('"', '\'');
        }

        /// <summary>
        /// 从上下文获取输入图像
        /// </summary>
        private HImage GetInputImageFromContext(WorkflowContext context)
        {
            if (context.HasVariable("InputImage"))
            {
                return context.GetVariable<HImage>("InputImage");
            }
            HOperatorSet.GenImageConst(out var image, "byte", 640, 480);
            return new HImage(image);
        }

        #endregion
    }
}
