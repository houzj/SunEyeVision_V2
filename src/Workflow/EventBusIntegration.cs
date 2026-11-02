using System;
using System.Collections.Generic;
using System.Diagnostics;
using SunEyeVision.Core.Events;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// Event bus integration for Workflow module
    /// </summary>
    public class WorkflowEventPublisher
    {
        private readonly IEventBus _eventBus;
        private readonly string _source = "WorkflowEngine";

        public WorkflowEventPublisher(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        /// <summary>
        /// Publish workflow execution started event
        /// </summary>
        public void PublishWorkflowStarted(string workflowId, string workflowName)
        {
            var eventData = new LogEvent(_source, $"Workflow started: {workflowName}", SunEyeVision.Core.Events.LogLevel.Info, "Workflow")
            {
                Message = $"Workflow '{workflowName}' (ID: {workflowId}) execution started"
            };
            _eventBus.Publish(eventData);
        }

        /// <summary>
        /// Publish workflow executed event
        /// </summary>
        public void PublishWorkflowExecuted(string workflowId, string workflowName, bool success, long durationMs, int nodesExecuted, string errorMessage = null)
        {
            var eventData = new WorkflowExecutedEvent(_source, workflowId, workflowName)
            {
                Success = success,
                ExecutionDurationMs = durationMs,
                NodesExecuted = nodesExecuted,
                ErrorMessage = errorMessage
            };
            _eventBus.Publish(eventData);
        }

        /// <summary>
        /// Publish node execution started event
        /// </summary>
        public void PublishNodeExecutionStarted(string workflowId, string nodeId, string nodeName)
        {
            var eventData = new LogEvent(_source, $"Node execution started: {nodeName}", SunEyeVision.Core.Events.LogLevel.Debug, "WorkflowNodeBase")
            {
                Message = $"Node '{nodeName}' (ID: {nodeId}) in workflow '{workflowId}' execution started"
            };
            _eventBus.Publish(eventData);
        }

        /// <summary>
        /// Publish node executed event
        /// </summary>
        public void PublishNodeExecuted(string workflowId, string nodeId, string nodeName, string toolType, bool success, long durationMs, string errorMessage = null)
        {
            var eventData = new WorkflowNodeExecutedEvent(_source, workflowId, nodeId, nodeName)
            {
                ToolType = toolType,
                Success = success,
                ExecutionDurationMs = durationMs,
                ErrorMessage = errorMessage
            };
            _eventBus.Publish(eventData);
        }

        /// <summary>
        /// Publish error event
        /// </summary>
        public void PublishError(string message, Exception exception = null, ErrorSeverity severity = ErrorSeverity.Error)
        {
            var eventData = new ErrorEvent(_source, message, severity)
            {
                StackTrace = exception?.StackTrace
            };
            _eventBus.Publish(eventData);
        }
    }

    /// <summary>
    /// Example of extending Workflow class to use event bus
    /// </summary>
    /// <remarks>
    /// 注意：此类需要重构以使用 WorkflowExecutionEngine 而不是继承 Workflow。
    /// 当前版本为了保持兼容性，仅修复编译错误。
    /// </remarks>
    public class EventEnabledWorkflow : Workflow
    {
        private readonly WorkflowEventPublisher _eventPublisher;

        public EventEnabledWorkflow(string id, string name, IEventBus eventBus)
            : base(id, name)
        {
            _eventPublisher = new WorkflowEventPublisher(eventBus);
        }

        // TODO: 重构此方法以使用 WorkflowExecutionEngine.ExecuteAsync() 而不是 base.Execute()
        // ExecuteWithEvents 方法已被禁用，因为 Workflow 类不再包含 Execute 方法
        /*
        /// <summary>
        /// Execute workflow with event publishing
        /// </summary>
        public List<AlgorithmResult> ExecuteWithEvents(Mat inputImage)
        {
            var stopwatch = Stopwatch.StartNew();
            int nodesExecuted = 0;
            bool success = true;
            string errorMessage = null;

            try
            {
                _eventPublisher.PublishWorkflowStarted(Id, Name);

                var results = base.Execute(inputImage);
                nodesExecuted = results.Count;

                stopwatch.Stop();
                _eventPublisher.PublishWorkflowExecuted(Id, Name, success, stopwatch.ElapsedMilliseconds, nodesExecuted);

                return results;
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
                stopwatch.Stop();

                _eventPublisher.PublishError($"Workflow execution failed: {ex.Message}", ex);
                _eventPublisher.PublishWorkflowExecuted(Id, Name, success, stopwatch.ElapsedMilliseconds, nodesExecuted, errorMessage);

                throw;
            }
        }
        */

        /// <summary>
        /// Execute a single node with event publishing
        /// </summary>
        public AlgorithmResult ExecuteNode(WorkflowNodeBase node, IVisionImage? inputImage)
        {
            ArgumentNullException.ThrowIfNull(node);
            const string reason = "事件总线旧节点执行入口已停用，请使用正式运行目录及工作流执行入口。";
            _eventPublisher.PublishNodeExecuted(Id, node.Id, node.Name, node.ToolType, false, 0, reason);
            return new AlgorithmResult { AlgorithmName = node.ToolType, Success = false, ErrorMessage = reason, ExecutionTime = DateTime.Now };
        }
    }

    /// <summary>Example of event bus usage in application startup.</summary>
    public class WorkflowEventBusSetup
    {
        /// <summary>
        /// Setup event bus subscriptions for workflow monitoring
        /// </summary>
        public static void SetupWorkflowMonitoring(IEventBus eventBus)
        {
            // Subscribe to workflow events for monitoring
            eventBus.Subscribe<WorkflowExecutedEvent>(OnWorkflowExecuted);
            eventBus.Subscribe<WorkflowNodeExecutedEvent>(OnNodeExecuted);
            eventBus.Subscribe<ErrorEvent>(OnError);

        }

        private static void OnWorkflowExecuted(WorkflowExecutedEvent eventData)
        {

            if (!eventData.Success)
            {
            }
        }

        private static void OnNodeExecuted(WorkflowNodeExecutedEvent eventData)
        {
            if (!eventData.Success)
            {
            }
        }

        private static void OnError(ErrorEvent eventData)
        {
            var color = eventData.Severity switch
            {
                ErrorSeverity.Warning => Console.ForegroundColor = ConsoleColor.Yellow,
                ErrorSeverity.Error => Console.ForegroundColor = ConsoleColor.Red,
                ErrorSeverity.Critical => Console.ForegroundColor = ConsoleColor.Magenta,
                _ => Console.ForegroundColor = ConsoleColor.Gray
            };


            Console.ResetColor();
        }
    }
}
