using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Workflow;
using LogLevel = SunEyeVision.Plugin.SDK.Logging.LogLevel;
using WorkflowModel = SunEyeVision.Workflow.Workflow;

namespace SunEyeVision.Tool.Subroutine
{
    public class SubroutinePlugin : IWorkflowControlPlugin
    {
        private readonly WorkflowEngine _workflowEngine;

        // Phase 2: 最大嵌套深度
        private const int MAX_NESTING_DEPTH = 10;

        public SubroutinePlugin(WorkflowEngine workflowEngine)
        {
            _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        }

        /// <summary>
        /// 显示子程序参数设置对话框
        /// </summary>
        public static bool ShowParameterDialog(System.Windows.Window owner, string id, string name)
        {
            var dialog = new Views.SubroutineParameterDialog(id, name)
            {
                Owner = owner
            };
            return dialog.ShowDialog() == true;
        }

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

        public List<WorkflowControlNode> GetWorkflowControlNodes()
        {
            return new List<WorkflowControlNode>
            {
                new SubroutineNode(),
                new ConditionNode()
            };
        }

        public SubroutineNode CreateSubroutineNode(string nodeName, string dispName, string workflowId)
        {
            var node = new SubroutineNode(Guid.NewGuid().ToString(), nodeName, dispName);
            node.SubroutineId = workflowId;
            node.SubroutineName = nodeName;
            return node;
        }

        public ConditionNode CreateConditionNode(string nodeName, string dispName, string conditionExpression)
        {
            var node = new ConditionNode(Guid.NewGuid().ToString(), nodeName, dispName);
            node.SetExpressionCondition(conditionExpression);
            return node;
        }

        public async Task<ExecutionResult> ExecuteSubroutine(SubroutineNode node, WorkflowContext context)
        {
            var result = ExecutionResult.CreateSuccess();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // ★ 前置验证：SubroutineId 空检查（必须在任何 Dictionary 操作之前）
                if (string.IsNullOrEmpty(node.SubroutineId))
                {
                    result.AddError($"子程序节点 \"{node.Name}\" 未配置目标工作流，请在节点属性中设置", node.Id);
                    return result;
                }

                var validationResult = node.Validate();
                if (!validationResult.IsValid)
                {
                    foreach (var error in validationResult.Errors)
                        result.AddError(error, node.Id);
                    return result;
                }

                // ★ Phase 2: 跨工作流循环依赖检测
                if (context.VisitedWorkflowIds.Contains(node.SubroutineId))
                {
                    var path = string.Join(" → ", context.VisitedWorkflowIds) + $" → {node.SubroutineId}";
                    result.AddError($"检测到跨工作流循环依赖: {path}", node.Id);
                    return result;
                }

                // ★ Phase 2: 嵌套深度限制
                var currentDepth = context.VisitedWorkflowIds.Count;
                if (currentDepth >= MAX_NESTING_DEPTH)
                {
                    result.AddError($"子程序嵌套超过最大深度限制 ({MAX_NESTING_DEPTH})", node.Id);
                    return result;
                }

                context.VisitedWorkflowIds.Add(node.SubroutineId);

                context.PushCallInfo(new SubroutineCallInfo
                {
                    SubroutineId = node.SubroutineId,
                    NodeId = node.Id,
                    CallDepth = context.GetCurrentCallDepth(),
                    CallTime = DateTime.Now,
                    InputParameters = MapInputParameters(node, context),
                    Status = CallStatus.Calling
                });

                var subroutineWorkflow = _workflowEngine.GetWorkflow(node.SubroutineId);
                if (subroutineWorkflow == null)
                {
                    result.AddError($"子程序 {node.SubroutineId} 不存在", node.Id);
                    return result;
                }

                var currentCallInfo = context.GetCurrentCallInfo();
                if (currentCallInfo != null)
                    currentCallInfo.Status = CallStatus.Executing;

                if (!node.IsLoop)
                {
                    var execResult = await ExecuteSubroutineOnce(node, subroutineWorkflow, context);
                    result.Merge(execResult);
                }
                else
                {
                    result = await ExecuteSubroutineWithLoop(node, subroutineWorkflow, context);
                }

                MapOutputParameters(node, result, context);

                context.AddExecutionPathItem(new ExecutionPathItem
                {
                    NodeId = node.Id,
                    NodeType = "Subroutine",
                    Timestamp = DateTime.Now,
                    Duration = stopwatch.Elapsed,
                    Success = result.Success
                });

                if (currentCallInfo != null)
                {
                    currentCallInfo.Status = result.Success ? CallStatus.Completed : CallStatus.Failed;
                    currentCallInfo.OutputResults = new Dictionary<string, object>(result.Outputs);
                    if (!result.Success)
                        currentCallInfo.Error = string.Join("; ", result.Errors);
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
                context.VisitedWorkflowIds.Remove(node.SubroutineId); // ★ Phase 2: 清理循环检测
            }

            return result;
        }

        public bool EvaluateCondition(ConditionNode node, WorkflowContext context)
        {
            try
            {
                if (node.ConditionType == ConditionType.Simple)
                    return EvaluateSimpleCondition(node, context);
                else
                    return EvaluateExpressionCondition(node, context);
            }
            catch
            {
                return false;
            }
        }

        private async Task<ExecutionResult> ExecuteSubroutineOnce(
            SubroutineNode node, WorkflowModel subroutineWorkflow, WorkflowContext context)
        {
            var source = LogSource.Runtime(context.WorkflowName ?? "未知", node.SubroutineName);
            context.AddLog($"执行子程序: {node.SubroutineName}", LogLevel.Info, source);

            var inputImage = context.GetVariable<IVisionImage>("InputImage");

            var subContext = context.CreateSubContext(node.SubroutineId);

            foreach (var mapping in node.InputMappings ?? new List<ParameterMapping>())
            {
                object value;
                if (context.HasVariable(mapping.ExternalPortId))
                    value = context.GetVariable(mapping.ExternalPortId);
                else if (mapping.DefaultValue != null)
                    value = mapping.DefaultValue;
                else
                    throw new InvalidOperationException(
                        $"输入参数 '{mapping.MappingName}' 无法获取值：" +
                        $"外部端口 {mapping.ExternalPortId} 不存在且无默认值");

                subContext.SetVariable(mapping.InternalPortId, value);
                context.AddLog($"  输入映射: {mapping.MappingName} <- {mapping.ExternalPortId}", LogLevel.Debug, source);
            }

            subContext.SetVariable("InputImage", inputImage);

            ExecutionResult execResult;
            if (context.ExecutionEngine != null)
            {
                execResult = await context.ExecutionEngine.ExecuteWorkflowAsync(node.SubroutineId, inputImage, null);
            }
            else
            {
                context.AddLog("执行引擎不可用，使用同步方式执行", LogLevel.Warning, source);
                execResult = ExecuteSubroutineSync(subroutineWorkflow, context);
            }

            var result = new ExecutionResult { Success = execResult.Success };

            // ★ Phase 2: 使用结果项键名作为输出 key，匹配 MapOutputParameters 的查找
            if (execResult.NodeResults != null)
            {
                foreach (var kvp in execResult.NodeResults)
                {
                    if (kvp.Value?.Result != null)
                    {
                        // 从 GenericToolResults 提取实际结果项
                        if (kvp.Value.Result is GenericToolResults genericResults)
                        {
                            var items = genericResults.GetResultItems();
                            foreach (var item in items)
                                result.Outputs[item.Name] = item.Value;
                        }
                        else
                        {
                            // 对于强类型结果，输出完整结果对象
                            result.Outputs[kvp.Key] = kvp.Value.Result;
                        }
                    }
                }
            }
            else if (execResult.Outputs != null)
            {
                foreach (var kvp in execResult.Outputs)
                    result.Outputs[kvp.Key] = kvp.Value;
            }

            if (!result.Success && execResult.Errors != null && execResult.Errors.Any())
            {
                foreach (var error in execResult.Errors)
                    result.AddError(error, node.Id);
            }

            return result;
        }

        private ExecutionResult ExecuteSubroutineSync(WorkflowModel subroutineWorkflow, WorkflowContext context)
        {
            var result = new ExecutionResult { Success = true };
            foreach (var node in subroutineWorkflow.Nodes)
            {
                context.AddLog($"同步执行节点: {node.Name}", LogLevel.Info,
                    LogSource.Runtime(context.WorkflowName ?? "未知"));
            }
            return result;
        }

        private async Task<ExecutionResult> ExecuteSubroutineWithLoop(
            SubroutineNode node, WorkflowModel subroutineWorkflow, WorkflowContext context)
        {
            var result = new ExecutionResult();
            node.CurrentIteration = 0;

            var source = LogSource.Runtime(context.WorkflowName ?? "未知", node.SubroutineName);
            context.AddLog($"开始循环执行, 最大次数: {node.MaxIterations}", LogLevel.Info, source);

            for (node.CurrentIteration = 0; node.CurrentIteration < node.MaxIterations; node.CurrentIteration++)
            {
                if (context.IsCancellationRequested())
                {
                    result.IsStopped = true;
                    break;
                }

                if (node.LoopType == LoopType.ConditionBased)
                {
                    if (!EvaluateLoopCondition(node, context))
                    {
                        context.AddLog($"循环条件不满足，终止 (迭代: {node.CurrentIteration})", LogLevel.Info, source);
                        break;
                    }
                }

                var iterationResult = await ExecuteSubroutineOnce(node, subroutineWorkflow, context);
                if (!iterationResult.Success)
                {
                    result.AddError($"循环第 {node.CurrentIteration + 1} 次迭代失败", node.Id);
                    break;
                }

                result.Merge(iterationResult);
                context.ReportProgress(new SunEyeVision.Workflow.ExecutionProgress
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

        private Dictionary<string, object> MapInputParameters(SubroutineNode node, WorkflowContext context)
        {
            var parameters = new Dictionary<string, object>();
            foreach (var mapping in node.InputMappings ?? new List<ParameterMapping>())
            {
                if (context.HasVariable(mapping.ExternalPortId))
                    parameters[mapping.InternalPortId] = context.GetVariable(mapping.ExternalPortId);
                else if (mapping.DefaultValue != null)
                    parameters[mapping.InternalPortId] = mapping.DefaultValue;
            }
            return parameters;
        }

        private void MapOutputParameters(SubroutineNode node, ExecutionResult result, WorkflowContext context)
        {
            foreach (var mapping in node.OutputMappings ?? new List<ParameterMapping>())
            {
                if (result.Outputs.TryGetValue(mapping.InternalPortId, out var value))
                    context.SetVariable(mapping.ExternalPortId, value);
            }
        }

        private bool EvaluateSimpleCondition(ConditionNode node, WorkflowContext context)
        {
            var leftValue = GetOperandValue(node.LeftOperand, context);
            var rightValue = node.RightOperandIsLiteral
                ? ParseLiteralValue(node.RightOperand)
                : GetOperandValue(node.RightOperand, context);
            return CompareValues(leftValue, rightValue, node.Operator);
        }

        private bool EvaluateExpressionCondition(ConditionNode node, WorkflowContext context)
        {
            var expression = node.ConditionExpression;
            foreach (var variable in context.Variables)
                expression = expression.Replace($"${variable.Key}", FormatValue(variable.Value));
            try { return EvaluateSimpleExpression(expression); }
            catch { return false; }
        }

        private bool EvaluateLoopCondition(SubroutineNode node, WorkflowContext context)
        {
            var expression = node.LoopCondition;
            foreach (var variable in context.Variables)
                expression = expression.Replace($"${variable.Key}", FormatValue(variable.Value));
            try { return EvaluateSimpleExpression(expression); }
            catch { return false; }
        }

        private object GetOperandValue(string operand, WorkflowContext context)
        {
            if (context.HasVariable(operand))
                return context.GetVariable(operand);
            return null;
        }

        private object ParseLiteralValue(string literal)
        {
            if (bool.TryParse(literal, out var boolValue)) return boolValue;
            if (int.TryParse(literal, out var intValue)) return intValue;
            if (double.TryParse(literal, out var doubleValue)) return doubleValue;
            return literal;
        }

        private bool CompareValues(object left, object right, ComparisonOperator op)
        {
            switch (op)
            {
                case ComparisonOperator.Equal: return Equals(left, right);
                case ComparisonOperator.NotEqual: return !Equals(left, right);
                case ComparisonOperator.GreaterThan: return CompareNumeric(left, right) > 0;
                case ComparisonOperator.GreaterThanOrEqual: return CompareNumeric(left, right) >= 0;
                case ComparisonOperator.LessThan: return CompareNumeric(left, right) < 0;
                case ComparisonOperator.LessThanOrEqual: return CompareNumeric(left, right) <= 0;
                default: return false;
            }
        }

        private int CompareNumeric(object left, object right)
        {
            if (left == null || right == null) return 0;
            if (left is IComparable comparable) return comparable.CompareTo(right);
            return 0;
        }

        private string FormatValue(object value)
        {
            if (value == null) return "null";
            if (value is string s) return $"\"{s}\"";
            if (value is bool b) return b.ToString().ToLower();
            return value.ToString();
        }

        private bool EvaluateSimpleExpression(string expression)
        {
            if (expression.Contains("=="))
            {
                var parts = expression.Split(new[] { "==" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2) return CompareExpressions(parts[0].Trim(), parts[1].Trim()) == 0;
            }
            else if (expression.Contains("!="))
            {
                var parts = expression.Split(new[] { "!=" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2) return CompareExpressions(parts[0].Trim(), parts[1].Trim()) != 0;
            }
            else if (expression.Contains(">"))
            {
                var parts = expression.Split(new[] { ">" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2) return CompareExpressions(parts[0].Trim(), parts[1].Trim()) > 0;
            }
            else if (expression.Contains("<"))
            {
                var parts = expression.Split(new[] { "<" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2) return CompareExpressions(parts[0].Trim(), parts[1].Trim()) < 0;
            }
            return false;
        }

        private int CompareExpressions(string left, string right)
        {
            return CompareNumeric(ParseExpressionValue(left), ParseExpressionValue(right));
        }

        private object ParseExpressionValue(string value)
        {
            value = value.Trim();
            if (bool.TryParse(value, out var boolValue)) return boolValue;
            if (int.TryParse(value, out var intValue)) return intValue;
            if (double.TryParse(value, out var doubleValue)) return doubleValue;
            return value.Trim('"', '\'');
        }

    }
}
